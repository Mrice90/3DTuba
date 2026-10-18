from pathlib import Path
import json
p=Path(__file__).resolve().parent
f=p/'source/adapter/NativeRulesSession.cs'
s=f.read_text(encoding='utf-8-sig')
start=s.index('        var selectedDeck=decks[fs[seat]];')
end=s.index('        deployment.Reveal(next.Board)',start)
s=s[:start]+'''        var selected = new[] { decks[fs[0]], decks[fs[1]] };
        var capitals = new[] { definitions[fs[0]=="ZEUS"?"zeus_capital_olympus_citadel":"poseidon_capital_atlantis_nexus"], definitions[fs[1]=="ZEUS"?"zeus_capital_olympus_citadel":"poseidon_capital_atlantis_nexus"] };
        if(request.TryGetProperty("online_setup",out var setup)) {
            if(!mp || Text(setup,"version")!="custom-decks-1" || !setup.TryGetProperty("players",out var players) || players.ValueKind!=JsonValueKind.Array || players.GetArrayLength()!=2)
                return Error(id,"BAD_REQUEST","Two negotiated deck setups are required",-1);
            for(int p=0;p<2;p++) {
                string error=ReadDeckBuild(players[p],out var build);
                if(error!=null)return Error(id,"BAD_REQUEST",error,-1);
                fs[p]=build.PrimaryFaction; selected[p]=build.Cards.ToList();capitals[p]=build.Capital;
            }
        } else if(request.TryGetProperty("human_setup",out var humanSetup)) {
            if(mp)return Error(id,"BAD_REQUEST","Online decks require both negotiated setups",-1);
            string error=ReadDeckBuild(humanSetup,out var build);
            if(error!=null)return Error(id,"BAD_REQUEST",error,-1);
            fs[seat]=build.PrimaryFaction;selected[seat]=build.Cards.ToList();capitals[seat]=build.Capital;
        } else if(request.TryGetProperty("human_deck",out var deckRequest)) {
            if(mp)return Error(id,"BAD_REQUEST","Online decks require both negotiated setups",-1);
            if(deckRequest.ValueKind!=JsonValueKind.Array)return Error(id,"BAD_REQUEST","human_deck must be an array",-1);
            selected[seat]=new List<CardDefinition>();
            foreach(var entry in deckRequest.EnumerateArray()) {
                if(entry.ValueKind!=JsonValueKind.String||!definitions.TryGetValue(entry.GetString(),out var definition)||definition.Faction!=fs[seat]||definition.Type==CardType.CAPITAL)
                    return Error(id,"BAD_REQUEST","Invalid legacy deck card",-1);
                selected[seat].Add(definition);
            }
            var errors=new DeckValidator().Validate(selected[seat]);
            if(errors.Count>0)return Error(id,"BAD_REQUEST",string.Join("; ",errors),-1);
        }
        var next=new MatchFactory().Create(seed,(new MatchRules(0,3,4,0,BoardGeometry.HEX) { PurchaseEconomy=true }).WithPlaytestPolicy(slots),selected[0],selected[1]);
        var deployment=new CapitalDeployment();
        for(int p=0;p<2;p++) {
            var card=new CardInstance(CardIdentity.FromName(seed+":capital:"+p),capitals[p],p,Zone.DECK);
            next.Register(card);deployment.Commit(p,card,!mp&&p==seat?new BoardPosition(x,y):new BoardPosition(BoardPosition.CenterX,p==0?0:BoardPosition.Height-1));
        }
'''+s[end:]
at=s.index('    bool Over=>')
s=s[:at]+'''    string ReadDeckBuild(JsonElement value,out DeckBuild build) {
        build=null;
        if(value.ValueKind!=JsonValueKind.Object)return "Deck setup must be an object";
        string primary=Text(value,"primaryFaction"),ally=Text(value,"allyFaction");
        if(!definitions.TryGetValue(Text(value,"capital"),out var capital))return "Choose a known Capital";
        if(!value.TryGetProperty("cards",out var entries)||entries.ValueKind!=JsonValueKind.Array)return "Deck cards must be an array";
        var cards=new List<CardDefinition>();
        foreach(var e in entries.EnumerateArray()) {
            if(e.ValueKind!=JsonValueKind.String||!definitions.TryGetValue(e.GetString(),out var card))return "Deck contains an unknown card";
            cards.Add(card);
        }
        var errors=DeckBuild.Errors(primary,string.IsNullOrEmpty(ally)?null:ally,capital,cards);
        if(errors.Count>0)return string.Join("; ",errors);
        build=new DeckBuild("Online deck",primary,string.IsNullOrEmpty(ally)?null:ally,capital,cards);return null;
    }
'''+s[at:]
f.write_text(s,encoding='utf-8')
catalog={}
for name in ['cards.json','card-faces.json']:
    for c in json.loads((p/'UnityProof/Assets/Playtest/Resources/Playtest'/name).read_text(encoding='utf-8-sig'))['cards']:
        catalog[c['id']]={k:c[k] for k in ['id','type','faction']}
(p/'net-lab/deck-catalog.json').write_text(json.dumps(list(catalog.values()),indent=2))
