import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {createRelaySession} from '../relay.js';
const a='aaaa',b='bbbb';
function setup(){const sent=[],broadcast=[];const session=createRelaySession({room:{roomId:'cosmetic-test',seatA:a,seatB:b},send:(seat,msg)=>sent.push({seat,...msg}),broadcast:msg=>broadcast.push(msg)});return {session,sent,broadcast,hello:seat=>session.handleMessage(seat,{type:'hello'}),choose:(seat,tile,extra={})=>session.handleMessage(seat,{type:'cosmetics',tile,rulesVersion:'purchase-v3-hex37',...extra})};}
test('both socket-bound seats receive identical preset ordering with no private cosmetic fields',()=>{
 const h=setup();h.hello(a);h.hello(b);h.choose(b,'Obsidian',{seat:a,coin:'steal',background:'steal'});assert.equal(h.broadcast.length,0);
 h.choose(a,'Moonstone');assert.deepEqual(h.broadcast,[{type:'cosmetics',rulesVersion:'purchase-v3-hex37',seat0:a,seat1:b,tile0:'Moonstone',tile1:'Obsidian'}]);
 assert.equal(h.session.snapshot().seq,0);assert.equal(h.session.snapshot().seedSet,false);
});
test('reject before hello, unknown presets and incompatible rules',()=>{
 const h=setup();h.choose(a,'Moonstone');assert.match(h.sent.at(-1).reason,/hello/);h.hello(a);
 for(const tile of ['../bad','https://example.com',null,'stormforge']){h.choose(a,tile);assert.match(h.sent.at(-1).reason,/unknown tile/);}
 h.choose(a,'Moonstone',{rulesVersion:'old'});assert.match(h.sent.at(-1).reason,/incompatible/);assert.equal(h.broadcast.length,0);
});
test('choices lock per seat and reconnect receives the retained snapshot',()=>{
 const h=setup();h.hello(a);h.hello(b);h.choose(a,'Moonstone');h.choose(b,'Tideglass');h.choose(a,'Obsidian');assert.match(h.sent.at(-1).reason,/locked/);
 h.session.handleDisconnect(a);h.hello(a);assert.equal(h.sent.at(-1).tile0,'Moonstone');assert.equal(h.sent.at(-1).tile1,'Tideglass');
 h.choose(a,'Moonstone');assert.equal(h.sent.at(-1).type,'cosmetics');assert.equal(h.broadcast.length,1);
});
test('unknown socket seat cannot choose for a player',()=>{
 const h=setup();h.hello(a);h.hello(b);h.choose('cccc','Moonstone');h.choose(a,'Stormforge');assert.equal(h.broadcast.length,0);h.choose(b,'Tideglass');assert.equal(h.broadcast[0].tile0,'Stormforge');
});
test('a legacy match already in play cannot add a late cosmetic choice',async()=>{
 const h=setup();h.hello(a);h.hello(b);
 for(const seat of [a,b]){
   const seed=seat,salt='salt';const commit=createHash('sha256').update(seed+'|'+salt).digest('hex');
   await h.session.handleMessage(seat,{type:'seed-commit',commit});await h.session.handleMessage(seat,{type:'seed-reveal',seed,salt});
 }
 assert.equal(h.session.snapshot().phase,'play');h.choose(a,'Moonstone');assert.match(h.sent.at(-1).reason,/before the match/);
});
test('hash checkpoint names the exact ordered action boundary',async()=>{
 const h=setup();h.hello(a);h.hello(b);
 for(const seat of [a,b]){const seed=seat,salt='salt';await h.session.handleMessage(seat,{type:'seed-commit',commit:createHash('sha256').update(seed+'|'+salt).digest('hex')});await h.session.handleMessage(seat,{type:'seed-reveal',seed,salt});}
 h.session.handleMessage(a,{type:'turn',turn:1,activeSeat:a});
 h.session.handleMessage(a,{type:'intent',actionId:'move'});h.session.handleMessage(a,{type:'intent',actionId:'end'});
 h.session.handleMessage(a,{type:'turn',turn:2,activeSeat:b});
 const checkpoint=h.broadcast.find(m=>m.type==='hash-request');assert.equal(checkpoint.turn,1);assert.equal(checkpoint.sequence,2);
 h.session.handleMessage(b,{type:'intent',actionId:'next'});assert.equal(checkpoint.sequence,2,'later actions do not shift the prior checkpoint');
});
