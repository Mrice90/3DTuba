import {readFileSync} from 'node:fs';
const catalog=new Map(JSON.parse(readFileSync(new URL('./deck-catalog.json',import.meta.url),'utf8')).map(c=>[c.id,c]));
export function validateDeck(value) {
    if(!value || !['ZEUS','POSEIDON'].includes(value.primaryFaction))throw Error('Choose a primary faction');
    const ally=value.allyFaction||null;
    if(ally&&(!['ZEUS','POSEIDON'].includes(ally)||ally===value.primaryFaction))throw Error('Choose at most one different ally faction');
    const capital=catalog.get(value.capital);
    if(!capital||capital.type!=='CAPITAL'||capital.faction!==value.primaryFaction)throw Error('Capital must belong to the primary faction');
    if(!Array.isArray(value.cards)||value.cards.length<1||value.cards.length>40)throw Error('Deck must contain 1 to 40 cards');
    const counts=new Map();
    for(const id of value.cards){const c=catalog.get(id);if(!c||c.type==='CAPITAL'||![value.primaryFaction,ally,'NEUTRAL'].includes(c.faction))throw Error('Deck cards must belong to the primary faction, one ally, or Neutral');counts.set(id,(counts.get(id)||0)+1);}
    if(counts.size<10||[...counts.values()].some(n=>n>4))throw Error('Use at least 10 different cards and at most four copies of each');
    // Exact draw order is part of the agreed setup; never sort or silently replace the user's list.
    return {primaryFaction:value.primaryFaction,allyFaction:ally,capital:value.capital,cards:[...value.cards]};
}
export function validateSetup(setup){
    if(setup?.version!=='custom-decks-1'||!Array.isArray(setup.players)||setup.players.length!==2)throw Error('Two negotiated player decks are required');
    return {version:'custom-decks-1',players:setup.players.map(validateDeck)};
}
