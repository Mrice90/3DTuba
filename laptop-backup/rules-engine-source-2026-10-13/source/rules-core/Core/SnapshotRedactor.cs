// Port of net-server/Redactor.java at TubaExperiment 992bc95.
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    public static class SnapshotRedactor
    {
        public static GameSnapshot Create(GameState state, int viewingPlayer)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (viewingPlayer < 0 || viewingPlayer > 1) throw new ArgumentException("Viewing player must be 0 or 1");
            var cards = new List<GameSnapshot.CardView>();
            foreach (var position in state.Board.Positions().OrderBy(p=>p.Y).ThenBy(p=>p.X))
                foreach (var id in state.Board.StackAt(position)) cards.Add(View(state.Card(id)!,position));
            for (int p=0;p<2;p++) foreach (var id in state.Player(p).Discard) cards.Add(View(state.Card(id)!,null));
            foreach (var id in state.Player(viewingPlayer).Hand) cards.Add(View(state.Card(id)!,null));
            int opponent=1-viewingPlayer;
            var hidden=state.Player(opponent).Hand.Concat(state.Player(opponent).Deck).Select(id=>id.ToString()).ToArray();
            var events=state.Events().Skip(Math.Max(0,state.Events().Count-80))
                .Where(e=>e.type!=GameEvent.Type.CARD_DRAWN && e.type!=GameEvent.Type.MULLIGAN_COMPLETED)
                .Where(e=>!hidden.Any(id=>e.detail.Contains(id,StringComparison.Ordinal))).ToList();
            return new GameSnapshot(state.Seed(),state.Rules().Geometry.ToString(),viewingPlayer,state.ActivePlayer,state.StartingPlayer(),
                state.TurnNumber(),new[]{state.PersonalTurnNumber(0),state.PersonalTurnNumber(1)},state.Phase().ToString(),state.Winner(),
                new[]{state.Player(0).CurrentGp,state.Player(1).CurrentGp},new[]{state.Player(0).MaximumGp,state.Player(1).MaximumGp},
                new[]{state.Player(0).Hand.Count,state.Player(1).Hand.Count},new[]{state.Player(0).Deck.Count,state.Player(1).Deck.Count},
                new[]{state.LandsPlayedThisTurn(0),state.LandsPlayedThisTurn(1)},new[]{state.StructuresPlayedThisTurn(0),state.StructuresPlayedThisTurn(1)},
                cards.AsReadOnly(),events.AsReadOnly(),state.IsMulliganWindowOpen());
        }

        private static GameSnapshot.CardView View(CardInstance card,BoardPosition? position) =>
            new GameSnapshot.CardView(card.InstanceId,card.Definition.Id,card.Owner,card.Zone,position,card.Damage,card.Tapped,
                card.AttackBonus,card.DefenseBonus,card.AttackedThisTurn,card.BlinkUsedThisTurn,card.AbilityUsedThisTurn,card.MovementSpent);
    }
}
