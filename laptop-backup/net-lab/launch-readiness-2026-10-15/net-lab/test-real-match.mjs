import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { writeFile } from 'node:fs/promises';
import { start } from './server.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../..');
const release = path.join(root, '3DTuba-restoration/releases/alpha-0.7.15-playable');
const cp = path.join(release, 'tools/rules-bridge/classes')+';'+path.join(release, 'infinite-conquest-alpha-0.7.15.jar');
const digest = s => createHash('sha256').update(s).digest('hex');

function mailbox() {
    const queue = []; const waits = [];
    return {
        push(value) { const waiter = waits.shift(); if (waiter) waiter(value); else queue.push(value); },
        next() {
            if (queue.length) return Promise.resolve(queue.shift());
            return new Promise((resolve, reject) => {
                const done = value => { clearTimeout(timer); resolve(value); };
                const timer = setTimeout(() => { const at = waits.indexOf(done); if(at>=0) waits.splice(at,1); reject(Error('Timed out waiting for protocol response')); }, 10000);
                waits.push(done);
            });
        }
    };
}
function engine() {
    const proc = spawn('java', ['-cp', cp, 'RulesBridge'], {windowsHide:true});
    const box = mailbox(); let sequence=0;
    createInterface({input:proc.stdout}).on('line', line => box.push(JSON.parse(line)));
    proc.stderr.on('data', data => process.stderr.write(data));
    return { async ask(op,args={}) { proc.stdin.write(JSON.stringify({id:String(++sequence),op,...args})+'\n'); return box.next(); }, close() { proc.kill(); } };
}
async function socket(url) {
    const ws = new WebSocket(url); const box = mailbox();
    ws.addEventListener('message', e => box.push(JSON.parse(e.data)));
    await new Promise((resolve,reject) => {ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
    return {send(message) {ws.send(JSON.stringify(message));}, next:()=>box.next(), close:()=>ws.close()};
}
const lab = await start({port:0});
const base = `http://127.0.0.1:${lab.port}`;
async function post(route,body) {
    const response=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
    assert.equal(response.status,200); return response.json();
}
const results=[];
try {
    for (const fixture of [42,713,101]) {
        const seats=[randomUUID(),randomUUID()]; const clients=[]; const engines=[];
        let hashChecks=0, reactions=0;
        try {
            for(const seat of seats) await post('/queue',{uuid:seat,name:'Launch local test',rating:1000});
            const room=await post('/v2/rooms/pair',{uuidA:seats[0],uuidB:seats[1],dataVersion:'lab-2'});
            for(const seat of seats) clients.push(await socket(`ws://127.0.0.1:${lab.port}/rooms/${room.roomId}/ws?dataVersion=lab-2&seat=${seat}`));
            for(let i=0;i<2;i++) clients[i].send({type:'hello',seat:seats[i]});
            for(const client of clients) assert.equal((await client.next()).type,'welcome');
            const seeds=[`fixture-${fixture}-a`,`fixture-${fixture}-b`];const salts=['salt-a','salt-b'];
            for(let i=0;i<2;i++) clients[i].send({type:'seed-commit',commit:digest(seeds[i]+'|'+salts[i])});
            // Ensure commits arrive before either reveal by sending on each ordered socket.
            for(let i=0;i<2;i++) clients[i].send({type:'seed-reveal',seed:seeds[i],salt:salts[i]});
            const agreed=await Promise.all(clients.map(c=>c.next()));
            assert.equal(agreed[0].type,'seed');assert.equal(agreed[0].seed,agreed[1].seed);
            const seed=parseInt(agreed[0].seed.slice(0,8),16)|0;
            engines.push(engine(),engine());
            let states=await Promise.all(engines.map((e,i)=>e.ask('new',{seed,human_player:i,human_faction:i?'POSEIDON':'ZEUS',bot_faction:i?'ZEUS':'POSEIDON',multiplayer:true,interactive_reactions:true})));
            assert(states.every(s=>s.ok));let turn=states[0].state.turn;
            clients[0].send({type:'turn',turn,activeSeat:seats[states[0].state.acting_player]});
            let actions=0;
            for(;actions<1800;actions++) {
                const hashes=await Promise.all(engines.map(e=>e.ask('hash')));assert.equal(hashes[0].state_hash,hashes[1].state_hash);hashChecks++;
                assert.deepEqual(states[0].legal,states[1].legal);
                if(states[0].state.phase==='GAME_OVER')break;
                const actor=states[0].state.acting_player;
                const legal=states[0].legal;assert(legal.length);
                const priority=states[0].state.reaction_pending?{cast:0,pass_reaction:1}:{attack:0,play:1,activate:2,move:3,blink:4,burrow:5,end_turn:6,cast:7};
                if(states[0].state.reaction_pending)reactions++;
                const best=Math.min(...legal.map(a=>priority[a.type]??20));
                const options=legal.filter(a=>(priority[a.type]??20)===best);
                const action=options[(actions*17+fixture)%options.length];
                clients[actor].send({type:'intent',actionId:action.id});
                const delivered=await Promise.all(clients.map(c=>c.next()));
                assert.deepEqual(delivered[0],delivered[1]);assert.equal(delivered[0].type,'intent');assert.equal(delivered[0].seq,actions+1);
                const sender=seats.indexOf(delivered[0].seat);assert.equal(sender,actor);
                states=await Promise.all(engines.map(e=>e.ask('act',{action_id:delivered[0].actionId,seat:sender})));
                assert(states.every(s=>s.ok),JSON.stringify(states));assert.deepEqual(states[0].events,states[1].events);
                if(states[0].state.turn>turn) {
                    const nextTurn=states[0].state.turn;
                    clients[0].send({type:'turn',turn:nextTurn,activeSeat:seats[states[0].state.acting_player]});
                    const requested=await Promise.all(clients.map(c=>c.next()));assert(requested.every(r=>r.type==='hash-request'));
                    const h=await Promise.all(engines.map(e=>e.ask('hash')));
                    for(let i=0;i<2;i++)clients[i].send({type:'hash',turn:requested[i].turn,hash:h[i].state_hash});
                    const verdict=await Promise.all(clients.map(c=>c.next()));assert(verdict.every(v=>v.type==='hash-ok'));
                    turn=nextTurn;
                }
            }
            assert.equal(states[0].state.phase,'GAME_OVER');
            results.push({fixture,seed,actions,hashChecks,reactions,winner:states[0].state.winner});console.log(JSON.stringify(results.at(-1)));
        } finally {for(const c of clients)c.close();for(const e of engines)e.close();}
    }
    await writeFile(path.resolve(here,'../relay-real-matches.json'),JSON.stringify({passed:true,scope:'Real local WebSockets and two independent rules engines; Unity online UI and Cloudflare deployment still unverified',matches:results},null,2));
} finally {await lab.close();}
