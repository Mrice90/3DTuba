import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { randomBytes } from 'node:crypto';
import { start } from '../server.js';
import { signRequest, b64url, playerIdFor } from '../ranked.js';

async function fixture(t) {
    const clock = { now: Date.now() };
    const app = await start({ port: 0, now: () => clock.now });
    const sockets = [];
    t.after(async () => {
        for (const socket of sockets) socket.destroy();
        for (const entry of app.relaySessions.values()) for (const ws of entry.sockets.values()) ws.close();
        await app.close();
    });
    async function call(player, method, path, data) {
        const body = data ? JSON.stringify(data) : '';
        const headers = await signRequest({ ...player, method, path, body, now: clock.now });
        const res = await app.ranked.fetch(new Request('http://local' + path, { method, headers, body: method === 'GET' ? undefined : body }));
        assert.ok(res.ok);
        return res.json();
    }
    async function player(name) {
        const keys = await crypto.subtle.generateKey({ name: 'Ed25519' }, true, ['sign', 'verify']);
        const pub = await crypto.subtle.exportKey('raw', keys.publicKey);
        const p = { privateKey: keys.privateKey, playerId: await playerIdFor(pub) };
        await call(p, 'POST', '/v3/players', { publicKey: b64url(pub), displayName: name });
        return p;
    }
    const a = await player('Alpha'), b = await player('Bravo'), stranger = await player('Stranger');
    await call(a, 'POST', '/v3/ranked/queue', { faction: 'zeus' });
    const mb = await call(b, 'POST', '/v3/ranked/queue', { faction: 'poseidon' });
    const ma = await call(a, 'GET', '/v3/ranked/queue');
    const room = JSON.parse(await app.kv.get(`v2:room:${ma.roomId}`));
    const path = seat => `/rooms/${ma.roomId}/ws?seat=${seat}&dataVersion=${ma.dataVersion}`;
    const headers = (p, target = path(ma.seat), extra = {}) => signRequest({ ...p, method: 'GET', path: target, now: clock.now, ...extra });
    const request = (target, hdr) => new Request('http://local' + target, { headers: hdr });
    async function upgrade(target, hdr = {}) {
        return new Promise((resolve, reject) => {
            const req = http.request({ host: '127.0.0.1', port: app.port, path: target, headers: {
                Connection: 'Upgrade', Upgrade: 'websocket', 'Sec-WebSocket-Version': '13',
                'Sec-WebSocket-Key': randomBytes(16).toString('base64'), ...hdr,
            }});
            req.setTimeout(3000, () => req.destroy(new Error('upgrade timed out')));
            req.on('upgrade', (res, socket) => { sockets.push(socket); socket.on('error', () => {}); resolve(res.statusCode); });
            req.on('response', res => { res.resume(); resolve(res.statusCode); });
            req.on('error', e => e.code === 'ECONNRESET' ? resolve(0) : reject(e));
            req.end();
        });
    }
    return { app, clock, a, b, stranger, ma, mb, room, path, headers, request, upgrade };
}

test('ranked socket signatures bind owner, seat, room, query, timestamp and one-time nonce', async t => {
    const h = await fixture(t), p = h.path(h.ma.seat);
    const authorize = (path, headers, room = h.room) => h.app.ranked.authorizeRelay(h.request(path, headers), room);
    assert.equal(h.ma.socketAuth, 'signed-upgrade-1');
    assert.equal(await authorize(p, {}), false);
    assert.equal(await authorize(p, await h.headers(h.b)), false, 'opponent knows seat but does not own it');
    assert.equal(await authorize(p, await h.headers(h.stranger)), false);
    assert.equal(await authorize(p, await h.headers(h.a, p, { privateKey: h.b.privateKey })), false);
    assert.equal(await authorize(p, await h.headers(h.a, p, { now: h.clock.now - 300001 })), false);
    assert.equal(await authorize(p, await h.headers(h.a, p, { now: h.clock.now + 300001 })), false);
    assert.equal(await authorize(p, { ...await h.headers(h.a), 'x-ic-signature': 'a' }), false);
    const hdr = await h.headers(h.a);
    assert.equal(await authorize(h.path(h.mb.seat), hdr), false);
    assert.equal(await authorize(p.replace(h.ma.dataVersion, 'wrong-version'), hdr), false);
    const wrongVersion = p.replace(h.ma.dataVersion, 'lab-3');
    assert.equal(await authorize(wrongVersion, await h.headers(h.a, wrongVersion)), false);
    assert.equal(await authorize(p.replace(h.ma.roomId, 'OTHERROOM'), hdr), false);
    assert.equal(await authorize(p, hdr), true);
    assert.equal(await authorize(p, hdr), false, 'replay rejected');
    const concurrent = await h.headers(h.a);
    assert.deepEqual((await Promise.all([authorize(p, concurrent), authorize(p, concurrent)])).sort(), [false, true]);
    assert.equal(await authorize(p, {}, { ...h.room, ranked: undefined }), false, 'old ranked rooms cannot downgrade');
    assert.equal(await authorize(p, {}, { ...h.room, roomId: 'MISSING' }), false, 'missing ranked record fails closed');
});

test('real ranked upgrades reject takeover, preserve live socket, and allow signed reconnect after rendezvous expiry', async t => {
    const h = await fixture(t), p = h.path(h.ma.seat);
    assert.equal(await h.upgrade(p), 0);
    assert.equal(h.app.relaySessions.size, 0);
    assert.equal(await h.upgrade(p, await h.headers(h.a)), 101);
    const entry = h.app.relaySessions.get(h.ma.roomId), original = entry.sockets.get(h.ma.seat);
    const bp = h.path(h.mb.seat);
    assert.equal(await h.upgrade(bp, await h.headers(h.a, bp)), 0);
    assert.equal(await h.upgrade(bp, await h.headers(h.b, bp)), 101);
    assert.equal(await h.upgrade(p, await h.headers(h.b)), 0);
    assert.equal(entry.sockets.get(h.ma.seat), original);
    assert.equal(original.closed, false);
    const signed = await h.headers(h.a);
    assert.equal(await h.upgrade(p, signed), 101);
    const replacement = entry.sockets.get(h.ma.seat);
    assert.notEqual(replacement, original);
    assert.equal(original.closed, true);
    assert.equal(await h.upgrade(p, signed), 0);
    assert.equal(entry.sockets.get(h.ma.seat), replacement);
    h.clock.now += 300001;
    assert.equal(await h.app.kv.get(`v2:room:${h.ma.roomId}`), null);
    assert.equal(await h.upgrade(p), 0);
    assert.equal(await h.upgrade(p, await h.headers(h.a)), 101);
    assert.equal(h.app.relaySessions.get(h.ma.roomId), entry);
});

test('room reaped during authentication cannot be resurrected by a delayed upgrade', async t => {
    const h = await fixture(t), p = h.path(h.ma.seat);
    assert.equal(await h.upgrade(p, await h.headers(h.a)), 101);
    const entry = h.app.relaySessions.get(h.ma.roomId);
    entry.session.end('timeout', h.ma.seat);
    h.clock.now += 300001;
    const originalAuthorize = h.app.ranked.authorizeRelay;
    h.app.ranked.authorizeRelay = async (...args) => {
        const accepted = await originalAuthorize(...args);
        // Deterministically interleave the timer's expired-terminal cleanup.
        h.app.relaySessions.delete(h.ma.roomId);
        for (const ws of entry.sockets.values()) ws.close();
        return accepted;
    };
    assert.equal(await h.upgrade(p, await h.headers(h.a)), 0);
    assert.equal(h.app.relaySessions.size, 0);
});

test('malformed HTTP signature is a clean authentication refusal', async t => {
    const h = await fixture(t);
    const res = await h.app.ranked.fetch(new Request('http://local/v3/ranked/queue', {
        headers: { ...await h.headers(h.a, '/v3/ranked/queue'), 'x-ic-signature': 'a' },
    }));
    assert.equal(res.status, 401);
});

test('legacy local rooms still connect but cross-origin upgrades are refused', async t => {
    const h = await fixture(t);
    const room = { roomId: 'LEGACY', seatA: 'aaaa', seatB: 'bbbb' };
    await h.app.kv.put('v2:room:LEGACY', JSON.stringify(room));
    const p = '/rooms/LEGACY/ws?seat=aaaa&dataVersion=lab-2';
    assert.equal(await h.upgrade(p, { Origin: 'https://attacker.example' }), 0);
    assert.equal(await h.upgrade(p, { Origin: 'http://127.0.0.1' }), 101);
});
