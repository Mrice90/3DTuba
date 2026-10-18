using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>Per-match port of the played Java structure-slots policy; no static mutable configuration.</summary>
    public sealed class SummonCapacityPolicy
    {
        private readonly IReadOnlyDictionary<string,string> _policy;
        public static SummonCapacityPolicy Disabled { get; } = new SummonCapacityPolicy(new Dictionary<string,string>());
        public SummonCapacityPolicy(IReadOnlyDictionary<string,string> policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var values = new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach (var pair in policy) values.Add(pair.Key,pair.Value.Trim());
            _policy = new ReadOnlyDictionary<string,string>(values);
            string enabled = Value("enabled","false");
            if (enabled != "true" && enabled != "false") throw new ArgumentException("Slot policy enabled must be true or false");
            foreach (var key in _policy.Keys.Where(k=>k!="enabled"))
            {
                int number = Integer(key,0);
                if (number<0) throw new ArgumentException("Negative slot value: "+key);
                if (key.EndsWith(".cost",StringComparison.Ordinal) && number==0) throw new ArgumentException("Characters require at least one slot: "+key);
            }
        }
        public bool Enabled => Value("enabled","false")=="true";
        public IReadOnlyDictionary<string,string> Policy => _policy;
        private string Value(string key,string fallback) => _policy.TryGetValue(key,out var value) ? value : fallback;
        private int Integer(string key,int fallback) => int.Parse(Value(key,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
        public int SlotCost(CardDefinition card) => card.Type==CardType.CHARACTER ? Integer("card."+card.Id+".cost",Integer("character.default.cost",1)) : 0;
        public int SlotSupply(CardDefinition card) => card.Type==CardType.STRUCTURE || card.Type==CardType.CAPITAL
            ? Integer("card."+card.Id+".supply",Integer(card.Type==CardType.STRUCTURE ? "structure.default.supply" : "capital.default.supply",0)) : 0;
        private int Count(GameState state,int owner,bool usage)
        {
            int total=0;var seen=new HashSet<Guid>();
            foreach (var at in state.Board.Positions()) foreach (var id in state.Board.StackAt(at))
            {
                if (!seen.Add(id)) continue;
                var card=state.Card(id) ?? throw new InvalidOperationException("Card not registered");
                if(card.Owner==owner && card.Zone==Zone.BATTLEFIELD) total+=usage ? SlotCost(card.Definition) : SlotSupply(card.Definition);
            }
            return total;
        }
        public int Total(GameState state,int owner) => Count(state,owner,false);
        public int Used(GameState state,int owner) => Count(state,owner,true);
        public int Available(GameState state,int owner) => Math.Max(0,Total(state,owner)-Used(state,owner));
        public string? Rejection(GameState state,int owner,CardDefinition card) => !Enabled || card.Type!=CardType.CHARACTER || SlotCost(card)<=Available(state,owner)
            ? null : "Not enough structure slots: requires "+SlotCost(card)+", available "+Available(state,owner);
    }
}
