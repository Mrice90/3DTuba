// Port of game-core/src/main/java/com/infiniteconquest/core/GameAction.java @ Desolate-Tuba e0e565b
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Java sealed interface → C# abstract record + derived records
    /// (CONVENTIONS.md §2). Java UUID card/target IDs → long (no Guid).
    /// CastSpell.TargetId is nullable: the Java engine explicitly null-checks
    /// it, and CastSpell.Destination is null-checked for teleport validation.
    /// </summary>
    public abstract record GameAction(int PlayerId)
    {
        public record PlayLand(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record PlayStructure(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record SummonCharacter(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record BurrowCharacter(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record MoveCharacter(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record BlinkCharacter(int PlayerId, long CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record Attack(int PlayerId, long AttackerId, long TargetId) : GameAction(PlayerId);
        public record CastSpell(int PlayerId, long CardId, long? TargetId, BoardPosition? Destination) : GameAction(PlayerId);
        public record ActivateAbility(int PlayerId, long CardId) : GameAction(PlayerId);
        public record EndTurn(int PlayerId) : GameAction(PlayerId);
    }
}
