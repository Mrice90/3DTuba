import {validateSetup} from './deck-setup.js';
// Native referee for the pinned purchase-v3-hex37 playtest. No client winner is trusted.
import { spawn } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const defaultRoot = fileURLToPath(new URL('../native/', import.meta.url));
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const PROFILE = 'purchase-v3-hex37';

export async function verifyNative(root = defaultRoot) {
    const manifest = JSON.parse(await readFile(path.join(root, 'manifest.json'), 'utf8'));
    if (manifest.profile !== PROFILE || !manifest.files || Object.keys(manifest.files).length < 5) throw Error('Unsupported engine manifest');
    for (const [name, expected] of Object.entries(manifest.files)) {
        const target = path.resolve(root, name);
        if (!target.startsWith(path.resolve(root) + path.sep) || sha(await readFile(target)) !== expected) throw Error('Native integrity check failed: ' + name);
    }
    return manifest;
}

// One bounded request at a time. A timeout, corrupt response or process failure never becomes a player loss.
export function openNative({ root = defaultRoot, dotnet = process.env.IC_DOTNET, timeoutMs = 10000 } = {}) {
    if (!dotnet) throw Error('IC_DOTNET must point to the .NET 8 host');
    const child = spawn(dotnet, [path.join(root, 'Runner.dll'), path.join(root, 'data')], { windowsHide: true, stdio: ['pipe','pipe','pipe'] });
    let buffer = '', pending = null, failed = null, closed = false;
    const fail = error => { failed = error; if (pending) { clearTimeout(pending.timer); pending.reject(error); pending = null; } };
    child.on('error', fail);
    child.on('exit', () => { closed = true; fail(Error('Native referee process exited')); });
    child.stdin.on('error', fail);
    child.stderr.on('data', () => {}); // Never mix diagnostic output with protocol responses.
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', text => {
        buffer += text;
        if (buffer.length > 4 * 1024 * 1024) { fail(Error('Native response exceeds limit')); child.kill(); return; }
        let end;
        while ((end = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, end); buffer = buffer.slice(end + 1);
            if (!pending) { fail(Error('Unexpected native response')); child.kill(); return; }
            const p = pending; pending = null; clearTimeout(p.timer);
            try { p.resolve(JSON.parse(line)); } catch { p.reject(Error('Malformed native response')); }
        }
    });
    return {
        request(value) {
            if (failed) return Promise.reject(failed);
            if (pending) return Promise.reject(Error('Concurrent native request'));
            return new Promise((resolve, reject) => {
                pending = { resolve, reject, timer: setTimeout(() => { fail(Error('Native request timed out')); child.kill(); }, timeoutMs) };
                child.stdin.write(JSON.stringify(value) + '\n');
            });
        },
        async close() {
            if (closed || !child.pid) return;
            const done = new Promise(resolve => child.once('exit', resolve));
            child.stdin.end();
            const timer = setTimeout(() => child.kill(), 1000);
            await done; clearTimeout(timer);
        }
    };
}

export function createNativeReplay(options = {}) {
    return async ({ seed, seats, intents, transcript }) => {
        if (!transcript || transcript.protocol !== 'relay-1' || transcript.rulesVersion !== PROFILE) throw Error('Unsupported transcript rules profile');
        const custom=transcript.setup?.version==='custom-decks-1'?validateSetup(transcript.setup):null;
        if (!custom && JSON.stringify(transcript.setup) !== JSON.stringify({factions:['zeus','poseidon'],decks:'faction-starters',capitals:'default'})) throw Error('Unsupported match setup');
        if (!Array.isArray(seats) || seats.length !== 2 || seats.some(s => typeof s !== 'string' || !s) || seats[0] === seats[1]) throw Error('Invalid seat mapping');
        if (typeof seed !== 'string' || !/^[0-9a-f]{64}$/i.test(seed)) throw Error('Invalid seed');
        if (!Array.isArray(intents) || intents.length > 20000) throw Error('Invalid intent count');
        // Structural corruption is a service failure, not evidence that either player cheated.
        for (let i=0; i<intents.length; i++) {
            const e=intents[i];
            if (!e || e.seq !== i+1 || !seats.includes(e.seat) || typeof e.actionId !== 'string' || !e.actionId || Buffer.byteLength(e.actionId)>512) throw Error('Malformed or non-contiguous transcript');
        }
        const pins=await verifyNative(options.root);
        const engine=PROFILE+':'+pins.files['InfiniteConquest.RulesCore.dll'].slice(0,16);
        const native=openNative(options);
        try {
            // Same unsigned-hex -> signed Int32 mapping as OnlineMatch.BeginOnline.
            let response=await native.request({op:'new',seed:parseInt(seed.slice(0,8),16)|0,human_player:0,human_faction:'ZEUS',bot_faction:'POSEIDON',difficulty:'HERO',interactive_reactions:true,multiplayer:true,client_metadata:true,...(custom?{online_setup:custom}:{})});
            if (!response.ok || !response.state) throw Error('Native setup failed');
            for (const e of intents) {
                const actor=seats.indexOf(e.seat);
                if (response.state.phase==='GAME_OVER') throw Error('Transcript continues after game over');
                if (actor !== response.state.acting_player || !response.legal?.some(a=>a.id===e.actionId)) {
                    return {legal:false,offendingSeat:e.seat,winnerSeat:seats[1-actor],engine};
                }
                response=await native.request({op:'act',seat:actor,action_id:e.actionId});
                if (!response.ok || !response.state) throw Error('Native engine rejected a listed legal move');
            }
            if (response.state.phase!=='GAME_OVER' || ![0,1].includes(response.state.winner)) throw Error('Incomplete match: no verified winner');
            const hash=await native.request({op:'hash'});
            if (!hash.ok || !/^[0-9a-f]{64}$/i.test(hash.state_hash)) throw Error('Native final hash unavailable');
            return {legal:true,winnerSeat:seats[response.state.winner],finalStateHash:hash.state_hash,engine};
        } finally { await native.close(); }
    };
}
export const replay = createNativeReplay();
