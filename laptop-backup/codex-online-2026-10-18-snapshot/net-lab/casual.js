// Local casual rendezvous. Match simulation and ranked authentication stay in their existing lanes.
import {validateDeck} from './deck-setup.js';
import { randomBytes, randomUUID } from 'node:crypto';
export function createCasualService(kv, now = Date.now) {
    const rooms = new Map();
    let tail = Promise.resolve();
    const json = (data, status = 200) => new Response(JSON.stringify(data), {status, headers:{'content-type':'application/json'}});
    const profile = {rulesVersion:'purchase-v3-hex37', decks:'custom-decks-1', capitals:'primary-faction'};
    function view(r, seat) {
        return {code:r.code, roomId:r.code, status:r.started?'matched':r.guest?'preparing':'waiting',
            seat, seat0:r.host, seat1:r.guest,
            faction:r.decks[seat]?.primaryFaction?.toLowerCase(), setup:r.started?{version:'custom-decks-1',players:[r.decks[r.host],r.decks[r.guest]]}:null,
            selfReady:r.ready.has(seat), opponentReady:r.ready.has(seat===r.host?r.guest:r.host),
            dataVersion:'lab-2', ...profile};
    }
    async function route(request) {
        for (const [id,r] of rooms) if(now()>=r.expires) rooms.delete(id);
        const url = new URL(request.url);
        let body={};
        if(request.method!=='GET') {try{body=await request.json();}catch{return json({error:'JSON body required'},400);}}
        if(request.method==='POST' && url.pathname==='/v2/casual') {
            if(!['zeus','poseidon'].includes(body.faction)||body.dataVersion!=='lab-2')return json({error:'Choose a supported faction and client version'},400);
            if(rooms.size>=1000)return json({error:'Lobby is full; retry later'},503);
            let code;do{code=randomBytes(5).toString('hex').toUpperCase();}while(rooms.has(code));
            let deck;try{deck=validateDeck(body.deck);}catch(e){return json({error:e.message},400);}
            const r={code,host:randomUUID(),guest:null,hostFaction:body.faction,ready:new Set(),started:false,expires:now()+600000};
            r.decks={[r.host]:deck};
            rooms.set(code,r);return json(view(r,r.host),201);
        }
        const match=url.pathname.match(/^\/v2\/casual\/([A-F0-9]{10})(?:\/(join|ready))?$/);
        if(!match)return json({error:'Unknown lobby route'},404);
        const r=rooms.get(match[1]);if(!r)return json({error:'Room expired or was closed. Ask the host for a new code.'},404);
        if(request.method==='POST' && match[2]==='join') {
            if(body.dataVersion!=='lab-2')return json({error:'Incompatible client version'},400);
            if(r.guest)return json({error:'Room is full'},409);
            let deck;try{deck=validateDeck(body.deck);}catch(e){return json({error:e.message},400);}
            r.guest=randomUUID();r.decks[r.guest]=deck;return json(view(r,r.guest));
        }
        const seat=request.method==='GET'?url.searchParams.get('seat'):body.seat;
        if(!seat || (seat!==r.host&&seat!==r.guest))return json({error:'Room membership required'},403);
        if(request.method==='DELETE') {
            if(r.started)return json({error:'Match already started'},409);
            rooms.delete(r.code);return json({ok:true});
        }
        if(request.method==='GET' && !match[2])return json(view(r,seat));
        if(request.method==='POST' && match[2]==='ready') {
            if(!r.guest)return json({error:'Wait for your opponent'},409);
            if(body.rulesVersion!==profile.rulesVersion||body.decks!==profile.decks||body.capitals!==profile.capitals)return json({error:'Match setup differs; update the client'},400);
            r.ready.add(seat);
            if(r.ready.size===2&&!r.started){
                const v=view(r,seat);
                await kv.put('v2:room:'+r.code,JSON.stringify({roomId:r.code,seatA:v.seat0,seatB:v.seat1,dataVersion:'lab-2',createdAt:now(),setup:{version:'custom-decks-1',players:[r.decks[r.host],r.decks[r.guest]]}}),{expirationTtl:600});
                r.started=true;
            }
            return json(view(r,seat));
        }
        return json({error:'Method not allowed'},405);
    }
    return {fetch(request){const pending=tail.then(()=>route(request));tail=pending.catch(()=>{});return pending;}};
}
