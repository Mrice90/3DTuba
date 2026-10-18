from pathlib import Path
p=Path(__file__).resolve().parent/'net-lab'
f=p/'casual.js';s=f.read_text();s=s.replace("import { randomBytes, randomUUID }", "import {validateDeck} from './deck-setup.js';\nimport { randomBytes, randomUUID }")
s=s.replace("seat0:r.hostFaction==='zeus'?r.host:r.guest, seat1:r.hostFaction==='poseidon'?r.host:r.guest,", "seat0:r.host, seat1:r.guest,")
s=s.replace("faction:seat===r.host?r.hostFaction:(r.hostFaction==='zeus'?'poseidon':'zeus'),", "faction:r.decks[seat]?.primaryFaction?.toLowerCase(), setup:r.started?{version:'custom-decks-1',players:[r.decks[r.host],r.decks[r.guest]]}:null,")
s=s.replace("            const r={code,host:","            let deck;try{deck=validateDeck(body.deck);}catch(e){return json({error:e.message},400);}\n            const r={code,host:")
s=s.replace("            rooms.set(code,r);", "            r.decks={[r.host]:deck};\n            rooms.set(code,r);")
s=s.replace("            r.guest=randomUUID();return", "            let deck;try{deck=validateDeck(body.deck);}catch(e){return json({error:e.message},400);}\n            r.guest=randomUUID();r.decks[r.guest]=deck;return")
s=s.replace("dataVersion:'lab-2',createdAt:now()}","dataVersion:'lab-2',createdAt:now(),setup:{version:'custom-decks-1',players:[r.decks[r.host],r.decks[r.guest]]}}")
f.write_text(s)
f=p/'server.js';s=f.read_text().replace('room: { roomId: room.roomId, seatA: room.seatA, seatB: room.seatB },','room: { ...room },');f.write_text(s)
f=p/'relay.js';s=f.read_text().replace('setup: { factions: ["zeus", "poseidon"], decks: "faction-starters", capitals: "default" },','setup: room.setup || { factions: ["zeus", "poseidon"], decks: "faction-starters", capitals: "default" },').replace('            sequence: state.seq,','            sequence: state.seq,\n            setup: room.setup || null,');f.write_text(s)
f=p/'ranked.js';s=f.read_text();s="import {validateDeck} from './deck-setup.js';\n"+s
s=s.replace('        const faction = String(data.faction || "").toLowerCase();','        const faction = String(data.faction || "").toLowerCase();\n        let deck=null;\n        if(data.deck){try{deck=validateDeck(data.deck);}catch(e){return bad(e.message);}\n            if(deck.primaryFaction.toLowerCase()!==faction)return bad("Primary faction must match the queue faction");\n            if(deck.allyFaction && !(await entitled(player.playerId,deck.allyFaction.toLowerCase())))return bad("Ally faction unavailable",403);\n        }')
s=s.replace('if (!other || other.playerId === player.playerId || other.faction === faction) continue;','if (!other || other.playerId === player.playerId || Boolean(other.deck)!==Boolean(deck) || (!deck && other.faction === faction)) continue;')
needle='                await putJson(k.room(room.roomId), room);'
s=s.replace(needle,'''                if(deck){room.players={A:other.playerId,B:player.playerId};room.factions={A:other.faction,B:faction};room.setup={version:'custom-decks-1',players:[other.deck,deck]};}
'''+needle)
s=s.replace('ranked: true, dataVersion, createdAt:', 'ranked: true, setup: room.setup, dataVersion, createdAt:')
s=s.replace('{ playerId: player.playerId, faction, at: now() }','{ playerId: player.playerId, faction, deck, at: now() }')
s=s.replace('seat0: room.seatA, seat1: room.seatB,','seat0: room.seatA, seat1: room.seatB, setup:room.setup||null,')
f.write_text(s)
f=p/'native-referee.js';s=f.read_text();s="import {validateSetup} from './deck-setup.js';\n"+s
s=s.replace("        if (JSON.stringify(transcript.setup)","        const custom=transcript.setup?.version==='custom-decks-1'?validateSetup(transcript.setup):null;\n        if (!custom && JSON.stringify(transcript.setup)")
s=s.replace("client_metadata:true});","client_metadata:true,...(custom?{online_setup:custom}:{})});")
f.write_text(s)
