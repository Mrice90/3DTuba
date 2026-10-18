using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;
using InfiniteConquest.RulesCore.Bot;

namespace InfiniteConquest.Native {
// In-process AI-079 boundary. A session owns all mutable rules, RNG and revision state.
public sealed class NativeRulesSession {
    readonly Dictionary<string,CardDefinition> definitions = new Dictionary<string,CardDefinition>();
    readonly Dictionary<string,List<CardDefinition>> decks = new Dictionary<string,List<CardDefinition>>();
    readonly SummonCapacityPolicy slots;
    GameState state;
    CommandProcessor commands;
    readonly GameEngine engine = new GameEngine();
    readonly NativeActionHints hints = new NativeActionHints();
    PlaytestBot[] bots;
    Projection projection;
    int human, revision, decisions;
    string[] factions;
    bool multiplayer, reactions, reactionPending, metadata, mulligan, mulliganPending;
    readonly Dictionary<string,string> actionCommands = new Dictionary<string,string>();
    List<Dictionary<string,object>> legal = new List<Dictionary<string,object>>();
    public NativeRulesSession(string directory) {
        foreach (var file in Directory.GetFiles(Path.Combine(directory,"cards"),"*.json").OrderBy(p=>p,StringComparer.Ordinal))
            foreach (var card in CardCatalog.LoadFromFile(file).Definitions()) definitions.Add(card.Id,card);
        foreach (var card in FactionTutorExpansion.Cards()) definitions.Add(card.Id,card);
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"faction-starters.json"))))
            foreach (var faction in new[]{"ZEUS","POSEIDON"})
                decks.Add(faction,doc.RootElement.GetProperty(faction).EnumerateArray().SelectMany(e=>Enumerable.Repeat(definitions[e.GetProperty("id").GetString()],e.GetProperty("copies").GetInt32())).ToList());
        slots = new SummonCapacityPolicy(File.ReadAllLines(Path.Combine(directory,"structure-slots.properties"))
            .Where(l=>l.Trim().Length>0&&!l.TrimStart().StartsWith("#")).Select(l=>l.Split(new[]{'='},2)).ToDictionary(p=>p[0].Trim(),p=>p[1].Trim()));
    }
    static Dictionary<string,object> Map(params object[] pairs) {
        var result = new Dictionary<string,object>();
        for(int i=0;i<pairs.Length;i+=2) result.Add((string)pairs[i],pairs[i+1]);
        return result;
    }
    static Dictionary<string,object> Hex(BoardPosition p)=>Map("x",p.X,"y",p.Y);
    static string Text(JsonElement request,string key,string fallback="")=>request.TryGetProperty(key,out var value)?value.ToString():fallback;
    static int Number(JsonElement request,string key,int fallback)=>request.TryGetProperty(key,out var value)&&value.TryGetInt32(out int n)?n:fallback;
    static bool Flag(JsonElement request,string key)=>request.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.True;
    Dictionary<string,object> Error(string id,string code,string message,int? rev=null)=>Map("id",id,"ok",false,"revision",rev??(state==null?-1:revision),"error_code",code,"error",message);
    Dictionary<string,object> Success(string id,List<Dictionary<string,object>> events)=>Map("id",id,"ok",true,"revision",revision,"events",events,"state",Snapshot(),"legal",legal);
    public string Handle(string line) {
        string id="";
        try {
            using(var doc=JsonDocument.Parse(line)) {
                var request=doc.RootElement; id=Text(request,"id");
                try {return JsonSerializer.Serialize(Handle(id,request));}
                catch(Exception e) {return JsonSerializer.Serialize(Error(id,"INTERNAL",e.GetType().Name+": "+e.Message));}
            }
        } catch(Exception e) {return JsonSerializer.Serialize(Error(id,"BAD_REQUEST","bad request: "+e.Message,-1));}
    }
    Dictionary<string,object> Handle(string id,JsonElement request) {
        string op=Text(request,"op");
        if(op=="new") return New(id,request);
        if(op!="legal"&&op!="hash"&&op!="act"&&op!="mulligan") return Error(id,"BAD_REQUEST","unknown op: "+op+" (want new|legal|act|mulligan|hash)");
        if(state==null) return Error(id,"NO_MATCH","no match started (send op:new first)");
        if(op=="legal") return Success(id,new List<Dictionary<string,object>>());
        if(op=="hash") return Map("id",id,"ok",true,"revision",revision,"turn",Math.Max(1,state.TurnNumber()),"state_hash",Hash());
        if(op=="mulligan") return Mulligan(id,request);
        if(op!="act") return Error(id,"BAD_REQUEST","unknown op: "+op+" (want new|legal|act|mulligan|hash)");
        if(multiplayer && (!request.TryGetProperty("seat",out var sender)||sender.ValueKind!=JsonValueKind.Number||sender.GetDouble()!=Actor))
            return Error(id,"INVALID_SEAT","Only the expected acting seat may submit an intent");
        string actionId=Text(request,"action_id");
        if(mulliganPending) return Error(id,"INVALID_ACTION","Complete Keep/Redraw before playing");
        if(Over) return Error(id,"INVALID_ACTION","match is over");
        if(!multiplayer&&!HumanTurn&&!reactionPending) return Error(id,"INVALID_ACTION","not the human turn");
        if(!actionCommands.TryGetValue(actionId,out string command)) return Error(id,"INVALID_ACTION","unknown or stale action id: "+actionId+" (revision "+revision+")");
        var events=new List<Dictionary<string,object>>();
        string result=Execute(command,events);
        if(!result.StartsWith("OK:",StringComparison.Ordinal)) return Error(id,"INVALID_ACTION","engine rejected action: "+result);
        revision++; RunBots(events); RefreshLegal(); return Success(id,events);
    }
    Dictionary<string,object> New(string id,JsonElement request) {
        long seed=request.TryGetProperty("seed",out var sn)&&sn.TryGetInt64(out long ns)?ns:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int seat=Number(request,"human_player",0);
        string hf=Text(request,"human_faction","ZEUS").ToUpperInvariant(),bf=Text(request,"bot_faction","POSEIDON").ToUpperInvariant();
        if(seat!=0&&seat!=1) return Error(id,"BAD_REQUEST","human_player must be 0 or 1",-1);
        if(!decks.ContainsKey(hf)||!decks.ContainsKey(bf)) return Error(id,"BAD_REQUEST","factions must be ZEUS or POSEIDON",-1);
        if(!Enum.TryParse(Text(request,"difficulty","HERO").ToUpperInvariant(),out BotDifficulty difficulty)||!Enum.IsDefined(typeof(BotDifficulty),difficulty)) return Error(id,"BAD_REQUEST","difficulty must be MORTAL, HERO or DEMIGOD",-1);
        int x=Number(request,"capital_x",BoardPosition.CenterX),y=Number(request,"capital_y",seat==0?0:BoardPosition.Height-1);
        if(BoardPosition.HomeSide(x,y)!=seat) return Error(id,"BAD_REQUEST","Capital must be on your half of the board");
        bool mp=Flag(request,"multiplayer"),mg=Flag(request,"interactive_mulligan");
        if(mp&&mg) return Error(id,"BAD_REQUEST","Interactive mulligan currently supports solo only");
        var fs=new string[2]; fs[seat]=hf; fs[1-seat]=bf;
        var selectedDeck=decks[fs[seat]];
        if(request.TryGetProperty("human_deck",out var deckRequest)) {
            if(mp) return Error(id,"BAD_REQUEST","Custom decks require negotiated online setup; this prototype supports custom solo decks only",-1);
            if(deckRequest.ValueKind!=JsonValueKind.Array) return Error(id,"BAD_REQUEST","human_deck must be an array",-1);
            selectedDeck=new List<CardDefinition>();
            foreach(var entry in deckRequest.EnumerateArray()) {
                if(entry.ValueKind!=JsonValueKind.String || !definitions.TryGetValue(entry.GetString(),out var definition) || definition.Faction.ToString()!=fs[seat] || definition.Type==CardType.CAPITAL)
                    return Error(id,"BAD_REQUEST","Deck contains an unknown, other-faction or Capital card",-1);
                selectedDeck.Add(definition);
            }
            var errors=new DeckValidator().Validate(selectedDeck);
            if(errors.Count>0) return Error(id,"BAD_REQUEST",string.Join("; ",errors),-1);
        }
        var next=new MatchFactory().Create(seed,(new MatchRules(0,3,4,0,BoardGeometry.HEX) { PurchaseEconomy=true }).WithPlaytestPolicy(slots),seat==0?selectedDeck:decks[fs[0]],seat==1?selectedDeck:decks[fs[1]]);
        var deployment=new CapitalDeployment();
        for(int p=0;p<2;p++) {
            var card=new CardInstance(CardIdentity.FromName(seed+":capital:"+p),definitions[fs[p]=="ZEUS"?"zeus_capital_olympus_citadel":"poseidon_capital_atlantis_nexus"],p,Zone.DECK);
            next.Register(card);deployment.Commit(p,card,!mp&&p==seat?new BoardPosition(x,y):new BoardPosition(BoardPosition.CenterX,p==0?0:BoardPosition.Height-1));
        }
        deployment.Reveal(next.Board);next.ActivateInitialCapitalPassive();
        state=next;human=seat;factions=fs;multiplayer=mp;mulligan=mg;mulliganPending=mg;reactions=Flag(request,"interactive_reactions");metadata=Flag(request,"client_metadata");reactionPending=false;
        commands=new CommandProcessor(state);bots=new[]{new PlaytestBot(difficulty,new SeededRng(seed^0xC0FFEE1L)),new PlaytestBot(difficulty,new SeededRng(seed^0xC0FFEE2L))};
        projection=new Projection(state,metadata);revision=0;decisions=0;RefreshLegal();
        if(metadata) projection.SnapshotDamage();
        var events=new List<Dictionary<string,object>>();RunBots(events);RefreshLegal();return Success(id,events);
    }
    bool Over=>state.Phase()==Phase.GAME_OVER;
    bool HumanTurn=>!Over&&state.ActivePlayer==human;
    int Actor=>mulliganPending?human:reactionPending?(multiplayer?1-state.ActivePlayer:human):state.ActivePlayer;
    Dictionary<string,object> Mulligan(string id,JsonElement request) {
        if(!mulliganPending)return Error(id,"INVALID_SETUP","No mulligan decision is pending");
        if(!request.TryGetProperty("seat",out var seat)||seat.ValueKind!=JsonValueKind.Number||seat.GetDouble()!=human) return Error(id,"INVALID_SEAT","Only the human seat may decide");
        if(!request.TryGetProperty("revision",out var rev)||rev.ValueKind!=JsonValueKind.Number||rev.GetDouble()!=revision) return Error(id,"INVALID_SETUP","Missing or stale revision");
        if(!request.TryGetProperty("instance_ids",out var values)||values.ValueKind!=JsonValueKind.Array||values.GetArrayLength()>3) return Error(id,"INVALID_SETUP","Select zero to three hand instance IDs");
        var ids=new List<Guid>();
        foreach(var v in values.EnumerateArray()) {
            if(v.ValueKind!=JsonValueKind.String)return Error(id,"INVALID_SETUP","Instance IDs must be strings");
            if(!Guid.TryParse(v.GetString(),out var guid)) return Error(id,"INVALID_SETUP","Invalid instance ID");
            if(guid.ToString()!=v.GetString()||ids.Contains(guid)||!state.Player(human).Hand.Contains(guid))return Error(id,"INVALID_SETUP","IDs must be unique current hand instances");
            ids.Add(guid);
        }
        state.Mulligan(human,ids);state.Mulligan(1-human,Array.Empty<Guid>());int count=state.Player(human).Hand.Count;
        mulliganPending=false;revision++;var events=new List<Dictionary<string,object>>();projection.DrainInto(events);RunBots(events);RefreshLegal();
        var response=Success(id,events);response.Add("mulligan_result",Map("seat",human,"replaced",ids.Count,"hand_count",count));return response;
    }
    void RefreshLegal() {
        actionCommands.Clear();legal=new List<Dictionary<string,object>>();
        if(mulliganPending||Over||(!multiplayer&&!HumanTurn&&!reactionPending))return;
        var forms=reactionPending?new List<string>(hints.SpellActionsForPlayer(state,Actor)):hints.ForActivePlayer(state,engine);
        if(reactionPending)forms.Add("pass");
        for(int i=0;i<forms.Count;i++) {
            var action=Describe(forms[i]);if(action==null)continue;
            string id="r"+revision+"-a"+i;action.Add("id",id);action.Add("command",forms[i]);actionCommands.Add(id,forms[i]);legal.Add(action);
        }
    }
    string Execute(string command,List<Dictionary<string,object>> events) {
        if(reactionPending) {
            if(command=="pass") {reactionPending=false;return "OK: reaction passed";}
            projection.SnapshotDamage();string r=commands.Execute(command);projection.DrainInto(events);if(r.StartsWith("OK:"))reactionPending=false;return r;
        }
        int p=state.ActivePlayer;projection.SnapshotDamage();string result=commands.Execute(command);projection.DrainInto(events);
        if(multiplayer) {if(result.StartsWith("OK:")&&command!="end"&&!Over)reactionPending=hints.SpellActionsForPlayer(state,1-state.ActivePlayer).Count>0;return result;}
        if(command!="end"&&!Over&&state.ActivePlayer==p&&p==human) {projection.SnapshotDamage();if(bots[1-p].React(state,commands,1-p)!=null)projection.DrainInto(events);}
        return result;
    }
    void RunBots(List<Dictionary<string,object>> events) {
        if(multiplayer||mulliganPending)return;
        while(!Over&&!HumanTurn&&!reactionPending) {
            if(state.TurnNumber()>300||++decisions>200000)throw new InvalidOperationException("match did not finish within limits");
            int p=state.ActivePlayer;projection.SnapshotDamage();var d=bots[p].TakeNextAction(state,commands,p);projection.DrainInto(events);
            if(d.Command!="end"&&!Over&&state.ActivePlayer==p) {
                int opp=1-p;
                if(opp==human&&reactions&&hints.SpellActionsForPlayer(state,human).Count>0) {reactionPending=true;return;}
                if(opp!=human) {projection.SnapshotDamage();if(bots[opp].React(state,commands,opp)!=null)projection.DrainInto(events);}
            }
        }
    }
    Dictionary<string,object> Describe(string form) {
        string prefix="react "+Actor+" ";if(form.StartsWith(prefix))form="cast "+form.Substring(prefix.Length);
        var parts=Regex.Split(form,"\\s+");var action=new Dictionary<string,object>();
        try {
            string verb=parts[0];int N(int n)=>int.Parse(parts[n]);
            if(verb=="buy") {
                var id=Guid.Parse(parts[1]);var c=state.Card(id);
                return Map("type","buy","instance_id",id.ToString(),"card_id",c.Definition.Id,"card_name",c.Definition.Name,"price",PurchaseRules.Price(c.Definition),"category",c.Definition.Type.ToString());
            }
            if(verb=="end"||verb=="pass")return Map("type",verb=="end"?"end_turn":"pass_reaction");
            action.Add("type",verb);
            if(verb=="play"||verb=="burrow"||verb=="cast") {
                int index=N(1);var guid=state.Player(Actor).Hand[index];var card=state.Card(guid);
                action.Add("hand_index",index);action.Add("card_id",card.Definition.Id);action.Add("card_name",card.Definition.Name);action.Add("instance_id",guid.ToString());
                var to=new BoardPosition(N(2),N(3));action.Add(verb=="cast"?"target":"to",Hex(to));
                if(verb=="cast") {action.Add("target_instance_id",state.Board.TopAt(to).Value.ToString());if(parts.Length==6){action.Add("has_destination",true);action.Add("destination",Hex(new BoardPosition(N(4),N(5))));}}
            } else if(verb=="move"||verb=="blink"||verb=="attack"||verb=="activate") {
                var from=new BoardPosition(N(1),N(2));var guid=verb=="activate"&&parts.Length==4?Guid.Parse(parts[3]):state.Board.TopAt(from).Value;var card=state.Card(guid);
                action.Add("instance_id",guid.ToString());action.Add("card_id",card.Definition.Id);action.Add(verb=="activate"?"at":"from",Hex(from));
                if(verb!="activate") {var to=new BoardPosition(N(3),N(4));action.Add("to",Hex(to));if(verb=="attack") {var target=state.Board.TopAt(to).Value;action.Add("target_instance_id",target.ToString());action.Add("target_card_id",state.Card(target).Definition.Id);}}
            } else return null;
        }catch {return null;}
        return action;
    }
    Dictionary<string,object> DisplayCard(Guid id,bool board) {
        var card=state.Card(id);var m=Map("card_id",card.Definition.Id,"instance_id",id.ToString(),"slot_cost",slots.SlotCost(card.Definition),"slot_supply",slots.SlotSupply(card.Definition));
        if(board) {m.Add("owner",card.Owner);if(metadata)m.Add("damage",card.Damage);}return m;
    }
    Dictionary<string,object> Snapshot() {
        var snap=Map("seed",state.Seed(),"turn",Math.Max(1,state.TurnNumber()),"phase",state.Phase().ToString(),"active_player",state.ActivePlayer,"winner",state.Winner(),"you",human,"revision",revision,"purchase_economy",true,"rules_version","purchase-v3-hex37","slots_enabled",slots.Enabled,"reaction_pending",reactionPending,"multiplayer",multiplayer,"acting_player",Actor,"starting_player",state.StartingPlayer());
        if(mulligan){snap.Add("setup_stage",mulliganPending?"MULLIGAN":"PLAY");snap.Add("mulligan",Map("pending",mulliganPending,"seat",human,"max_replacements",3,"bot_policy","KEEP","replacement_destination","DISCARD"));}
        var players=new List<object>();for(int p=0;p<2;p++) {var pl=state.Player(p);players.Add(Map("seat",p,"faction",factions[p],"controller",multiplayer||p==human?"human":"bot","gp",pl.CurrentGp,"personal_turn",state.PersonalTurnNumber(p),"slots_total",slots.Total(state,p),"slots_used",slots.Used(state,p),"slots_available",slots.Available(state,p),"hand_count",pl.Hand.Count,"hand",p==human?pl.Hand.Select(g=>DisplayCard(g,false)).ToList():new List<Dictionary<string,object>>(),"deck_count",pl.Deck.Count,"discard_count",pl.Discard.Count));}
        snap.Add("players",players);
        snap.Add("market",state.Player(human).Deck.GroupBy(g=>state.Card(g).Definition.Id).Select(g=> {
            var c=state.Card(g.First()); return Map("card_id",c.Definition.Id,"name",c.Definition.Name,"category",c.Definition.Type.ToString(),"price",PurchaseRules.Price(c.Definition),"remaining",g.Count(),"bought",state.Purchases(human,c.Definition.Type));
        }).ToList());
        snap.Add("board",state.Board.Positions().Where(p=>state.Board.StackAt(p).Count>0).Select(p=>Map("x",p.X,"y",p.Y,"stack",state.Board.StackAt(p).Select(g=>DisplayCard(g,true)).ToList())).ToList());
        if(metadata){snap.Add("capitals",Capitals());snap.Add("abilities",Abilities(human));}return snap;
    }
    CardInstance Capital(int player)=>player<0?null:state.BattlefieldCards(player).FirstOrDefault(c=>c.Definition.Type==CardType.CAPITAL&&state.Board.PositionOf(c.InstanceId)!=null);
    List<object> Capitals() {
        var output=new List<object>();for(int p=0;p<2;p++){var c=Capital(p);if(c==null)output.Add(Map("seat",p,"hp",0,"destroyed",true));else {var at=state.Board.PositionOf(c.InstanceId);output.Add(Map("seat",p,"instance_id",c.InstanceId.ToString(),"card_id",c.Definition.Id,"max_hp",c.Definition.HitPoints,"damage",c.Damage,"hp",Math.Max(0,c.Definition.HitPoints-c.Damage),"x",at.X,"y",at.Y));}}return output;
    }
    List<object> Abilities(int player) {
        var output=new List<object>();var byId=legal.Where(a=>(string)a["type"]=="activate").GroupBy(a=>(string)a["instance_id"]).ToDictionary(g=>g.Key,g=>(string)g.First()["id"]);
        bool yourTurn=!Over&&!mulliganPending&&!reactionPending&&state.ActivePlayer==player&&(multiplayer||player==human);
        foreach(var pos in state.Board.Positions().OrderBy(p=>p.Y).ThenBy(p=>p.X))foreach(var id in state.Board.StackAt(pos)) {
            var c=state.Card(id);if(c.Owner!=player||c.Zone!=Zone.BATTLEFIELD)continue;
            var abilities=c.Definition.Abilities.Where(a=>a.Trigger==AbilityTrigger.ACTIVATED).ToList();if(abilities.Count==0)continue;
            bool buried=id!=state.Board.TopAt(pos),aim=abilities.Any(a=>a.Effect==AbilityEffectType.DAMAGE_ENEMY_CAPITAL),heal=abilities.Any(a=>a.Effect==AbilityEffectType.HEAL_CAPITAL);int cost=abilities.Sum(a=>a.GpCost);
            string status="READY",reason="";byId.TryGetValue(id.ToString(),out string actionId);
            if(!yourTurn){status="BLOCKED";reason=Over?"The match is over":mulliganPending?"Finish Keep/Redraw first":reactionPending?"Resolve the reaction window first":"Wait for your turn";}
            else if(buried&&c.Definition.Type!=CardType.LAND&&c.Definition.Type!=CardType.STRUCTURE){status="BLOCKED";reason="Only the top card of a stack can activate an ability";}
            else if(c.AbilityUsedThisTurn){status="USED";reason="Ability already used this turn";}
            else if(aim&&!new LineOfSightRules().HasLineToEnemyCapital(state,c,pos)){status="BLOCKED";reason="No line of sight to the enemy Capital - blocked by cover";}
            else if(state.Player(player).CurrentGp<cost){status="BLOCKED";reason="Needs "+cost+" GP; you have "+state.Player(player).CurrentGp;}
            else if(actionId==null){status="BLOCKED";reason="Not offered by the rules engine right now";}
            var m=Map("instance_id",id.ToString(),"card_id",c.Definition.Id,"name",c.Definition.Name,"type",c.Definition.Type.ToString(),"x",pos.X,"y",pos.Y,"buried",buried,"effect",string.Join("; ",abilities.Select(a=>Effect(a.Effect,a.Amount))),"effect_type",abilities[0].Effect.ToString(),"gp_cost",cost,"status",status,"reason",reason);
            if(status=="READY"&&actionId!=null)m.Add("action_id",actionId);
            var target=Capital(aim?1-player:heal?player:-1);if((aim||heal)&&target!=null)m.Add("target",Hex(state.Board.PositionOf(target.InstanceId)));output.Add(m);
        }return output;
    }
    static string Effect(AbilityEffectType effect,int amount) {
        switch(effect) {
            case AbilityEffectType.DRAW_CARD:return "Draw "+amount+(amount==1?" random card":" random cards")+" from the remaining full deck (free)";
            case AbilityEffectType.DRAW_CHARACTER:return "Draw "+amount+" Character"+(amount==1?"":"s");
            case AbilityEffectType.DRAW_STRUCTURE:return "Draw "+amount+" Structure"+(amount==1?"":"s");
            case AbilityEffectType.GAIN_GP:return "Gain "+amount+" GP";
            case AbilityEffectType.HEAL_SELF:return "Repair "+amount+" damage on this card";
            case AbilityEffectType.HEAL_CAPITAL:return "Heal your Capital "+amount+" HP";
            case AbilityEffectType.BUFF_SELF_ATTACK:return "+"+amount+" attack";
            case AbilityEffectType.BUFF_SELF_DEFENSE:return "+"+amount+" defense";
            case AbilityEffectType.DAMAGE_ENEMY_CAPITAL:return "Deal "+amount+" damage to the enemy Capital. Needs line of sight to the enemy Capital";
            default:return effect+" "+amount;
        }
    }
    Dictionary<string,object> EncodeCard(Guid id) {
        var c=state.Card(id);if(c==null)return Map("instance_id",id.ToString(),"missing",true);
        return Map("instance_id",id.ToString(),"card_id",c.Definition.Id,"owner",c.Owner,"zone",c.Zone.ToString(),"damage",c.Damage,"combat_damage",c.CombatDamage,"tapped",c.Tapped,"movement_spent",c.MovementSpent,"attacked",c.AttackedThisTurn,"blink_used",c.BlinkUsedThisTurn,"attack_bonus",c.AttackBonus,"defense_bonus",c.DefenseBonus,"ability_used",c.AbilityUsedThisTurn,"summoned_turn",c.SummonedTurn);
    }
    string Hash() {
        var root=Map("rules_version","purchase-v3-hex37","rng_state",state.Rng.State,"purchases",Enumerable.Range(0,2).Select(p=>Enumerable.Range(0,5).Select(t=>state.Purchases(p,(CardType)t)).ToArray()).ToArray(),"seed",state.Seed(),"structure_slot_policy",slots.Policy.ToDictionary(p=>p.Key,p=>(object)p.Value),"turn",Math.Max(1,state.TurnNumber()),"phase",state.Phase().ToString(),"active_player",state.ActivePlayer,"winner",state.Winner());
        if(mulligan)root.Add("mulligan_pending",mulliganPending);
        root.Add("players",Enumerable.Range(0,2).Select(p=>Map("seat",p,"faction",factions[p],"gp",state.Player(p).CurrentGp,"max_gp",state.Player(p).MaximumGp,"hand",state.Player(p).Hand.Select(EncodeCard).ToList(),"deck",state.Player(p).Deck.Select(EncodeCard).ToList(),"discard",state.Player(p).Discard.Select(EncodeCard).ToList())).ToList());
        root.Add("board",state.Board.Positions().OrderBy(p=>p.X).ThenBy(p=>p.Y).Where(p=>state.Board.StackAt(p).Count>0).Select(p=>Map("x",p.X,"y",p.Y,"stack",state.Board.StackAt(p).Select(EncodeCard).ToList())).ToList());
        using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical(root))).Select(b=>b.ToString("x2")));
    }
    static string Canonical(object value) {
        if(value is IDictionary<string,object> map)return "{"+string.Join(",",map.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>Canonical(p.Key)+":"+Canonical(p.Value)))+"}";
        if(value is string s) return JsonSerializer.Serialize(s,new JsonSerializerOptions {Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        if(value is IEnumerable sequence) return "["+string.Join(",",sequence.Cast<object>().Select(Canonical))+"]";
        return JsonSerializer.Serialize(value);
    }
    sealed class Projection {
        readonly GameState state;readonly bool metadata;
        readonly Dictionary<Guid,int[]> damage=new Dictionary<Guid,int[]>();readonly Dictionary<Guid,BoardPosition> last=new Dictionary<Guid,BoardPosition>();int consumed;
        readonly List<Guid> damageOrder=new List<Guid>();int damageCapacity=16;
        readonly List<Dictionary<string,object>> output=new List<Dictionary<string,object>>();
        static readonly Regex Single=new Regex(@"^([0-9a-fA-F-]{36})$"),Moved=new Regex(@"^([0-9a-fA-F-]{36}) BoardPosition\[x=(\d+), ?y=(\d+)\] -> BoardPosition\[x=(\d+), ?y=(\d+)\] cost (\d+)$"),Pair=new Regex(@"^([0-9a-fA-F-]{36}) -> ([0-9a-fA-F-]{36})( at (\d+),(\d+))?$"),Leading=new Regex(@"^(\d+)\b"),Ability=new Regex(@"^([0-9a-fA-F-]{36}) \S+ \S+ (\d+)"),Terrain=new Regex(@"^([0-9a-fA-F-]{36}) ([0-9a-fA-F-]{36}) \S+ (\d+)( (\d+),(\d+))?$");
        public Projection(GameState state,bool metadata) {this.state=state;this.metadata=metadata;foreach(var c in Battlefield()) {var p=state.Board.PositionOf(c.InstanceId);if(p!=null)last[c.InstanceId]=p;}}
        IEnumerable<CardInstance> Battlefield()=>state.BattlefieldCards(0).Concat(state.BattlefieldCards(1));
        public void SnapshotDamage(){damage.Clear();damageOrder.Clear();foreach(var c in Battlefield()){damage.Add(c.InstanceId,new[]{c.Damage,c.CombatDamage});damageOrder.Add(c.InstanceId);}while(damage.Count>damageCapacity*3/4)damageCapacity*=2;}
        static uint JavaUuidHash(Guid id) {string s=id.ToString("N");uint h=0;for(int i=0;i<32;i+=8)h^=Convert.ToUInt32(s.Substring(i,8),16);return h^(h>>16);}
        Dictionary<string,object> Base(int player)=>Map("turn",Math.Max(1,state.TurnNumber()),"player",player);
        public void DrainInto(List<Dictionary<string,object>> sink) {
            int mark=output.Count,gameOver=-1;bool trigger=false,spell=false;var events=state.Events();
            while(consumed<events.Count){var e=events[consumed++];var wire=Adapt(e);if(wire!=null){if(e.type==GameEvent.Type.GAME_OVER)gameOver=output.Count;output.Add(wire);}
                if(e.type==GameEvent.Type.ATTACK_RESOLVED||e.type==GameEvent.Type.OPPORTUNITY_ATTACK||e.type==GameEvent.Type.TERRAIN_TRIGGERED)trigger=true;
                if(e.type==GameEvent.Type.CARD_PLAYED&&Guid.TryParse(e.detail.Trim(),out var id)&&state.Card(id)?.Definition.Type==CardType.SPELL)spell=true;
            }
            if(trigger||spell||metadata){var synth=new List<Dictionary<string,object>>();var seen=new HashSet<Guid>();foreach(var c in Battlefield()){seen.Add(c.InstanceId);Synthetic(c,synth);}foreach(var id in damageOrder.OrderBy(g=>JavaUuidHash(g)&(uint)(damageCapacity-1)))if(!seen.Contains(id)&&state.Card(id)!=null)Synthetic(state.Card(id),synth);if(gameOver>=0)output.InsertRange(gameOver,synth);else output.AddRange(synth);}
            if(metadata)SnapshotDamage();sink.AddRange(output.Skip(mark));for(int i=0;i<sink.Count;i++)sink[i]["seq"]=i;
        }
        void Synthetic(CardInstance c,List<Dictionary<string,object>> sink) {
            damage.TryGetValue(c.InstanceId,out var before);int delta=c.Damage+c.CombatDamage-(before==null?0:before[0]+before[1]);bool capital=c.Definition.Type==CardType.CAPITAL;
            if(metadata&&before!=null&&capital&&c.Damage<before[0]) {int healed=before[0]-c.Damage;var m=Base(c.Owner);m.Add("event","CAPITAL_HEALED");m.Add("instance_id",c.InstanceId.ToString());m.Add("amount",healed);m.Add("detail",healed+" capital healing to "+c.Definition.Id);sink.Add(m);return;}
            if(delta>0){var m=Base(c.Owner);m.Add("event",capital?"CAPITAL_HIT":"DAMAGE_DEALT");m.Add("instance_id",c.InstanceId.ToString());m.Add("amount",delta);m.Add("detail",delta+(capital?" capital damage to ":" damage to ")+c.Definition.Id);sink.Add(m);}
        }
        Dictionary<string,object> Adapt(GameEvent e) {
            var m=Base(e.playerId);string d=e.detail;Match match=null;string G(int n)=>match.Groups[n].Value;int N(int n)=>int.Parse(G(n));
            m.Add("event",e.type.ToString());m.Add("detail",d);
            switch(e.type) {
                case GameEvent.Type.MATCH_STARTED:case GameEvent.Type.TURN_STARTED:case GameEvent.Type.TURN_ENDED:case GameEvent.Type.PHASE_CHANGED:case GameEvent.Type.GAME_OVER:return m;
                case GameEvent.Type.CARD_DRAWN:case GameEvent.Type.CARD_PLAYED:case GameEvent.Type.CARD_DESTROYED:case GameEvent.Type.EXHAUSTION_DAMAGE:
                    match=Single.Match(d.Trim());if(!match.Success)return null;m.Add("instance_id",G(1));var c=state.Card(Guid.Parse(G(1)));
                    if(e.type==GameEvent.Type.CARD_DESTROYED)m.Add("card_id",c==null?"unknown:"+G(1):c.Definition.Id);
                    if(e.type==GameEvent.Type.EXHAUSTION_DAMAGE)m.Add("amount",1);
                    if(e.type==GameEvent.Type.CARD_PLAYED){if(c!=null){m.Add("card_id",c.Definition.Id);var p=state.Board.PositionOf(c.InstanceId);if(p!=null){m.Add("to",Hex(p));last[c.InstanceId]=p;}}if(!m.ContainsKey("to")){var cap=state.BattlefieldCards(e.playerId).FirstOrDefault(k=>k.Definition.Type==CardType.CAPITAL&&state.Board.PositionOf(k.InstanceId)!=null);if(cap!=null)m.Add("to",Hex(state.Board.PositionOf(cap.InstanceId)));}}return m;
                case GameEvent.Type.CHARACTER_MOVED:
                    match=Moved.Match(d.Trim());if(!match.Success)return null;m.Add("instance_id",G(1));m.Add("from",Hex(new BoardPosition(N(2),N(3))));var to=new BoardPosition(N(4),N(5));m.Add("to",Hex(to));m.Add("amount",N(6));last[Guid.Parse(G(1))]=to;return m;
                case GameEvent.Type.ATTACK_RESOLVED:case GameEvent.Type.OPPORTUNITY_ATTACK:
                    match=Pair.Match(d.Trim());if(!match.Success)return null;m.Add("instance_id",G(1));m.Add("target_instance_id",G(2));var target=Guid.Parse(G(2));var pos=state.Board.PositionOf(target);if(pos==null)last.TryGetValue(target,out pos);if(pos!=null)m.Add("to",Hex(pos));return m;
                case GameEvent.Type.GP_GENERATED:case GameEvent.Type.GP_SPENT:match=Leading.Match(d.Trim());if(match.Success)m.Add("amount",N(1));return m;
                case GameEvent.Type.CARD_ABILITY_TRIGGERED:
                    match=Ability.Match(d.Trim());if(!match.Success||(!metadata&&match.Length!=d.Trim().Length))return null;m.Add("instance_id",G(1));m.Add("amount",N(2));return m;
                case GameEvent.Type.TERRAIN_TRIGGERED:
                    match=Terrain.Match(d.Trim());if(!match.Success)return null;m.Add("instance_id",G(1));m.Add("target_instance_id",G(2));m.Add("amount",N(3));return m;
                default:return null;
            }
        }
    }
}
}
