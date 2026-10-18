using System;
// Port of game-core/src/main/java/com/infiniteconquest/core/GameAction.java @ Desolate-Tuba e0e565b
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Java sealed interface → C# abstract record + derived records
    /// (CONVENTIONS.md §2). Java UUID card/target IDs are preserved as deterministic full Guids.
    /// CastSpell.TargetId is nullable: the Java engine explicitly null-checks
    /// it, and CastSpell.Destination is null-checked for teleport validation.
    /// </summary>
    public abstract record GameAction(int PlayerId)
    {
        public record PlayLand(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record PlayStructure(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record SummonCharacter(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record BurrowCharacter(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record MoveCharacter(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record BlinkCharacter(int PlayerId, Guid CardId, BoardPosition Destination) : GameAction(PlayerId);
        public record Attack(int PlayerId, Guid AttackerId, Guid TargetId) : GameAction(PlayerId);
        public record CastSpell(int PlayerId, Guid CardId, Guid? TargetId, BoardPosition? Destination) : GameAction(PlayerId);
        public record ActivateAbility(int PlayerId, Guid CardId) : GameAction(PlayerId);
        public record BuyCard(int PlayerId, Guid CardId) : GameAction(PlayerId);
        public record EndTurn(int PlayerId) : GameAction(PlayerId);
    }
}
