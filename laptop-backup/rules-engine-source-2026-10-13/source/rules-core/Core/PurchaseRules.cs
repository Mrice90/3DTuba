using System;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core {
    public static class PurchaseRules {
        public static int Price(CardDefinition card) => card.Type == CardType.LAND ? 1
            : card.Type == CardType.STRUCTURE ? Math.Max(2 + card.Abilities.Count(a => a.Trigger == AbilityTrigger.ACTIVATED), card.DevelopmentGoldCost)
            : Math.Max(1, card.Cost);
        public static bool CanBuy(GameState state, int player, CardInstance card) =>
            state.Rules().PurchaseEconomy && state.Phase() == Phase.PLAY && player == state.ActivePlayer
            && card.Owner == player && card.Zone == Zone.DECK && state.Player(player).Deck.Contains(card.InstanceId)
            && card.Definition.Type != CardType.CAPITAL && state.Purchases(player, card.Definition.Type) == 0
            && state.Player(player).CurrentGp >= Price(card.Definition);
        public static ActionResult Buy(GameState state, GameAction.BuyCard action) {
            var card = state.Card(action.CardId);
            if (card == null || !CanBuy(state, action.PlayerId, card))
                return ActionResult.Rejected("Buy requires a remaining deck copy, enough gold and an unused category purchase this turn");
            state.SpendGp(action.PlayerId, Price(card.Definition), "purchase " + card.Definition.Name);
            state.Player(action.PlayerId).Purchase(card.InstanceId);
            card.MoveTo(Zone.HAND);
            state.RecordPurchase(action.PlayerId, card.Definition.Type);
            state.RecordPurchased(card);
            return ActionResult.Accepted("Purchased " + card.Definition.Name + "; playing it is free");
        }
        public static int BotScore(GameState state, int player, Guid id) {
            var c = state.Card(id)!;
            return c.Definition.Type == CardType.LAND ? 85 : c.Definition.Type == CardType.STRUCTURE ? 65
                : c.Definition.Type == CardType.CHARACTER ? 60 + c.Definition.Attack : 20;
        }
    }
}
