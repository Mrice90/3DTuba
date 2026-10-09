// Port of game-core/src/main/java/com/infiniteconquest/core/GameEvent.java @ Desolate-Tuba e0e565b
namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Wire-facing event record. Property names stay camelCase to match the
    /// AI-062 event JSON (CONVENTIONS.md §5) — this is the conformance surface.
    /// Enum members keep UPPER_SNAKE for wire parity (CONVENTIONS.md §2).
    /// </summary>
    public record GameEvent(long sequence, int turnNumber, int playerId, Type type, string detail)
    {
        public enum Type
        {
            MATCH_STARTED, MULLIGAN_COMPLETED, PHASE_CHANGED, TURN_STARTED, CARD_DRAWN, DRAW_FAILED,
            EXHAUSTION_DAMAGE, GP_GENERATED, GP_SPENT, CARDS_UNTAPPED, CARD_PLAYED, CHARACTER_MOVED,
            ATTACK_RESOLVED, OPPORTUNITY_ATTACK, CARD_DESTROYED, CAPITALS_REVEALED, CAPITAL_PASSIVE_TRIGGERED,
            DEVELOPMENT_PASSIVE_TRIGGERED,
            CARD_ABILITY_TRIGGERED, TERRAIN_TRIGGERED,
            GAME_OVER, TURN_ENDED
        }
    }
}
