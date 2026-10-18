import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { start } from './server.js';

const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(here,'../../..');
const exe=path.join(root,'playtest/unity-build-poseidon30k-2026-10-15/InfiniteConquestPlaytest.exe');
const output=path.resolve(root,'reviews/launch-readiness-2026-10-15/online-'+Date.now());
await mkdir(output);
const lab=await start({port:0});
const base=`http://127.0.0.1:${lab.port}`;
const processes=[];
const tiles=process.env.IC_SWAP?['Obsidian','Moonstone']:['Moonstone','Obsidian'];
const coins=process.env.IC_SWAP?['AbyssalBronze','Moonsteel']:['Moonsteel','AbyssalBronze'];
try {
    const seats=[randomUUID(),randomUUID()];
    async function post(route,body) {
        const r=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
        assert.equal(r.status,200);return r.json();
    }
    for(const seat of seats)await post('/queue',{uuid:seat,name:'Unity integration test',rating:1000});
    const room=await post('/v2/rooms/pair',{uuidA:seats[0],uuidB:seats[1],dataVersion:'lab-2'});
    const outcomes=await Promise.all(seats.map((seat,i)=>new Promise((resolve,reject)=>{
        const display=process.env.IC_RENDER?['-screen-width','960','-screen-height','640','-screen-fullscreen','0','-coinCapture',path.join(output,`coin-${i}`)]:['-batchmode','-nographics'];
        const command=process.env.IC_VISIBLE_PY||exe;
        const prefix=process.env.IC_VISIBLE_PY?[path.join(here,'visible_player.py'),exe]:[];
        const child=spawn(command,[...prefix,...display,'-onlineAutoSmoke','-relayBase',`ws://127.0.0.1:${lab.port}`,
            '-relayRoom',room.roomId,'-relaySeat0',seats[0],'-relaySeat1',seats[1],'-humanSeat',String(i),
            '-onlineTile',tiles[i],'-coinSkin',coins[i],'-onlineApplyDelayMs',i===0?'250':'0','-bridgeTranscript',path.join(output,`seat-${i}-bridge.jsonl`),'-playtestResult',path.join(output,`seat-${i}.json`),'-logFile',path.join(output,`seat-${i}.log`)],{cwd:path.dirname(exe),windowsHide:true});
        processes.push(child);
        const timer=setTimeout(()=>{child.kill();reject(Error(`Unity seat ${i} timed out`));},270000);
        child.on('error',error=>{clearTimeout(timer);reject(error);});
        child.on('exit',(code,signal)=>{clearTimeout(timer);resolve({seat:i,code,signal});});
    })));
    const results=await Promise.all(seats.map((_,i)=>readFile(path.join(output,`seat-${i}.json`),'utf8').then(JSON.parse)));
    await writeFile(path.join(output,'summary.json'),JSON.stringify({outcomes,results},null,2));
    assert(outcomes.every(o=>o.code===0),JSON.stringify(outcomes));
    assert(results.every(r=>r.passed),JSON.stringify(results));
    assert.equal(results[0].state_hash,results[1].state_hash);
    assert.equal(results[0].sequence,results[1].sequence);
    assert.equal(results[0].winner,results[1].winner);
    assert(results.some(r=>r.checkpointWaits>0),'test must exercise queued actions before a hash checkpoint');
    for(let i=0;i<2;i++){
        assert.equal(results[i].cosmeticsReady,true);assert.equal(results[i].tile0,tiles[0]);assert.equal(results[i].tile1,tiles[1]);assert.equal(results[i].localCoin,coins[i]);
        const log=await readFile(path.join(output,`seat-${i}.log`),'utf8');assert(log.includes('skin='+coins[i]+' '),'actual coin must use viewer choice');
    }
    console.log(JSON.stringify({passed:true,output,results}));
}finally{for(const process of processes)if(process.exitCode===null)process.kill();await lab.close();}



