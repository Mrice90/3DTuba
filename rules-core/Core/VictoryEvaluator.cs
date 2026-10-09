// Port of game-core/src/main/java/com/infiniteconquest/core/VictoryEvaluator.java @ Desolate-Tuba e0e565b
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class VictoryEvaluator
    {
        /// <summary>True if the player still has a Capital on the battlefield.</summary>
        public bool HasCapital(GameState state, int playerId) =>
            state.Board.Positions()
                .SelectMany(position => state.Board.StackAt(position))
                .Select(id => state.Card(id) ?? throw new System.InvalidOperationException("Card not registered: " + id))
                .Any(card => card.Owner == playerId && card.Definition.Type == CardType.CAPITAL);

        /// <summary>
        /// Evaluate immediately after the named player loses a permanent.
        /// Destroying the enemy Capital is the win condition: the game ends when
        /// the affected player has no Capital left, no matter how many lands and
        /// structures they still hold. Returns the winner's player id, or -1 if
        /// the game continues.
        /// </summary>
        public int WinnerAfterCapitalLoss(GameState state, int affectedPlayerId)
        {
            if (HasCapital(state, affectedPlayerId)) return -1;
            return 1 - affectedPlayerId;
        }
    }
}
