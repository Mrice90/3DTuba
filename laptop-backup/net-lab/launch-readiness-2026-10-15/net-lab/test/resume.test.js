import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {createRelaySession, MatchRoom} from '../relay.js';

const a='aaaa', b='bbbb';
function harness(){
 const clock={t:1000}, out={[a]:[],[b]:[]};
 const session=createRelaySession({room:{roomId:'resume',seatA:a,seatB:b},now:()=>clock.t,
  send:(seat,msg)=>out[seat].push(msg),broadcast:msg=>{out[a].push(msg);out[b].push(msg);}});
 return {clock,out,session,send:(seat,msg)=>session.handleMessage(seat,msg)};
}
async function play(h){
 for(const seat of [a,b]){
  h.send(seat,{type:'hello'});
  await h.send(seat,{type:'seed-commit',commit:createHash('sha256').update(seat+'|salt').digest('hex')});
  await h.send(seat,{type:'seed-reveal',seed:seat,salt:'salt'});
 }
}

test('invalid resume positions reject without marking disconnected seat connected',async()=>{
 const h=harness();await play(h);h.send(a,{type:'intent',actionId:'one'});
 for(const lastSeq of [-1,1.5,'1',2,Number.MAX_SAFE_INTEGER+1,null]){
  h.session.handleDisconnect(b);h.send(b,{type:'hello',lastSeq});
  assert.equal(h.out[b].at(-1).type,'error',String(lastSeq));
  assert.equal(h.session.snapshot().connected[b],false);
 }
 h.send(b,{type:'hello'});assert.deepEqual(h.out[b].at(-1).log.map(e=>e.seq),[1]);
});

test('resume resends unanswered checkpoints at the original sequence after replay',async()=>{
 const h=harness();await play(h);
 h.send(a,{type:'turn',turn:1,activeSeat:a});h.session.handleDisconnect(b);
 h.send(a,{type:'intent',actionId:'end'});h.send(a,{type:'turn',turn:2,activeSeat:b});
 h.send(a,{type:'hash',turn:1,hash:'a'.repeat(64)});
 h.send(a,{type:'intent',actionId:'later'});h.out[b].length=0;
 h.send(b,{type:'hello',lastSeq:0});
 assert.equal(h.out[b][0].type,'welcome');
 assert.deepEqual(h.out[b][0].log.map(e=>e.seq),[1,2]);
 assert.equal(h.out[b][0].sequence,2);assert.equal(h.out[b][0].activeSeat,b);
 assert.equal(h.out[b][0].msRemaining,120000);
 assert.deepEqual(h.out[b][1],{type:'hash-request',turn:1,sequence:1});
 h.send(b,{type:'hash',turn:1,hash:'a'.repeat(64)});
 assert.equal(h.out[a].at(-1).type,'hash-ok');
 h.out[b].length=0;h.session.handleDisconnect(b);h.send(b,{type:'hello',lastSeq:2});
 assert.equal(h.out[b].length,1,'resolved checkpoints are not reissued');
});

test('a seat that already answered does not receive its pending checkpoint again',async()=>{
 const h=harness();await play(h);h.send(a,{type:'turn',turn:1,activeSeat:a});
 h.send(a,{type:'turn',turn:2,activeSeat:b});h.send(a,{type:'hash',turn:1,hash:'a'.repeat(64)});
 h.out[a].length=0;h.session.handleDisconnect(a);h.send(a,{type:'hello'});
 assert.deepEqual(h.out[a].map(m=>m.type),['welcome']);
});

test('resume does not extend the turn deadline and late hello reports the forfeit',async()=>{
 const h=harness();await play(h);h.send(a,{type:'turn',turn:1,activeSeat:a});
 h.clock.t+=90000;h.session.handleDisconnect(a);h.send(a,{type:'hello'});
 assert.equal(h.out[a].findLast(m=>m.type==='welcome').msRemaining,30000);
 h.clock.t+=30001;h.send(a,{type:'hello'});
 assert.equal(h.session.snapshot().endReason,'timeout');
 assert.deepEqual(h.out[a].at(-1),{type:'forfeit',seat:a,reason:'timeout'});
});

test('an intent after the deadline cannot revive the turn before the timer tick',async()=>{
 const h=harness();await play(h);h.send(a,{type:'turn',turn:1,activeSeat:a});
 h.clock.t+=120001;h.send(a,{type:'intent',actionId:'late'});
 assert.equal(h.session.snapshot().seq,0);assert.equal(h.session.snapshot().endReason,'timeout');
});

test('replacement connection must hello before sending actions',async()=>{
 const h=harness();await play(h);assert.equal(h.session.handleConnect(a),true);
 h.send(a,{type:'intent',actionId:'no-hello'});
 assert.equal(h.session.snapshot().seq,0);assert.match(h.out[a].at(-1).reason,/hello/);
});

test('old Durable Object socket callbacks cannot disconnect or act as its replacement',async()=>{
 const saved={WebSocketPair:globalThis.WebSocketPair,Response:globalThis.Response};
 const servers=[];
 class Socket{
  constructor(){this.listeners={};this.messages=[];}
  addEventListener(name,cb){this.listeners[name]=cb;}
  send(text){this.messages.push(JSON.parse(text));}
  close(){this.closed=true;}
 }
 try{
  globalThis.WebSocketPair=class{constructor(){this[0]={};this[1]=new Socket();servers.push(this[1]);}};
  globalThis.Response=class{constructor(body,options){Object.assign(this,options);}};
  const room={roomId:'resume',seatA:a,seatB:b};
  const driver=new MatchRoom({storage:{get:async()=>room,put:async()=>{},setAlarm:async()=>{}},acceptWebSocket(){}},{});
  const request={headers:new Headers({'Upgrade':'websocket','X-Relay-Room':'resume','X-Relay-Seat':a})};
  await driver.fetch(request);const old=servers[0];old.listeners.message({data:JSON.stringify({type:'hello'})});
  await driver.fetch(request);const fresh=servers[1];fresh.listeners.message({data:JSON.stringify({type:'hello'})});
  const session=driver.sessions.get('resume');
  old.listeners.close();old.listeners.error();
  assert.equal(driver.sockets.get(a),fresh);assert.equal(session.snapshot().connected[a],true);
  const count=fresh.messages.length;old.listeners.message({data:JSON.stringify({type:'hello'})});
  assert.equal(fresh.messages.length,count,'superseded socket cannot send to the session');
  assert.equal(old.closed,true);
  session.end('timeout',a);
  assert.equal(driver.sessionFor(room),session,'terminal room does not start over in this instance');
  fresh.listeners.message({data:JSON.stringify({type:'hello'})});
  assert.deepEqual(fresh.messages.at(-1),{type:'forfeit',seat:a,reason:'timeout'});
 }finally{Object.assign(globalThis,saved);}
});
