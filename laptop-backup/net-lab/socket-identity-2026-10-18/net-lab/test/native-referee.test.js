import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {createNativeReplay} from '../native-referee.js';
const replay=createNativeReplay();
const a=JSON.parse(await readFile(new URL('./fixtures/fixed-negotiated-1791934412880.json',import.meta.url)));
const b=JSON.parse(await readFile(new URL('./fixtures/fixed-rendered-1791934434732.json',import.meta.url)));
const call=t=>replay({...t,transcript:t});
for(const [i,f] of [a,b].entries()) test('native replay matches saved real two-client game '+i,async()=>{
    const result=await call(f.transcript);
    assert.equal(result.legal,true);assert.equal(result.winnerSeat,f.expected.winnerSeat);assert.equal(result.finalStateHash,f.expected.finalStateHash);
});
test('claims cannot override the actual winner or final state',async()=>{
    const t=structuredClone(a.transcript);t.results=Object.fromEntries(t.seats.map(s=>[s,{winnerSeat:t.seats.find(x=>x!==a.expected.winnerSeat),finalHash:'f'.repeat(64)}]));
    const r=await call(t);assert.equal(r.winnerSeat,a.expected.winnerSeat);assert.equal(r.finalStateHash,a.expected.finalStateHash);
});
test('illegal action and wrong actor are attributed to the actual submitting seat',async()=>{
    let t=structuredClone(a.transcript);t.intents[0].actionId='fabricated';
    let r=await call(t);assert.equal(r.legal,false);assert.equal(r.offendingSeat,t.intents[0].seat);
    t=structuredClone(a.transcript);t.intents[0].seat=t.seats[1-t.seats.indexOf(t.intents[0].seat)];
    r=await call(t);assert.equal(r.legal,false);assert.equal(r.offendingSeat,t.intents[0].seat);
});
test('incomplete game, unknown profile, setup, seed and corrupt sequence never produce a winner',async()=>{
    const cases=[t=>t.intents=t.intents.slice(0,4),t=>t.rulesVersion='other',t=>t.setup.decks='custom',t=>t.seed='123',t=>t.intents[0].seq=2,t=>t.intents[0].seat='outsider'];
    for(const change of cases){const t=structuredClone(a.transcript);change(t);await assert.rejects(call(t));}
});
test('post-game actions are a corrupt transcript, not a second player loss',async()=>{
    const t=structuredClone(a.transcript);t.intents.push({...t.intents[0],seq:t.intents.length+1});await assert.rejects(call(t),/after game over/);
});
test('missing native runtime leaves the match unresolved',async()=>{
    await assert.rejects(createNativeReplay({dotnet:'missing-native-host'})({...a.transcript,transcript:a.transcript}));
});
