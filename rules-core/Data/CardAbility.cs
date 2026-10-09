// Port of game-core/src/main/java/com/infiniteconquest/core/CardAbility.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java record CardAbility(AbilityTrigger trigger, AbilityEffectType effect,
    /// int amount, int gpCost), including the compact-constructor validation.
    /// (Java's requireNonNull on the enum components is vacuous in C# — enums are
    /// non-nullable value types — so only the range checks are ported.)
    /// </summary>
    public record CardAbility
    {
        public AbilityTrigger Trigger { get; }
        public AbilityEffectType Effect { get; }
        public int Amount { get; }
        public int GpCost { get; }

        public CardAbility(AbilityTrigger trigger, AbilityEffectType effect, int amount, int gpCost)
        {
            if (amount <= 0) throw new ArgumentException("Ability amount must be positive", nameof(amount));
            if (gpCost < 0) throw new ArgumentException("Ability GP cost cannot be negative", nameof(gpCost));
            if (trigger != AbilityTrigger.ACTIVATED && gpCost != 0)
                throw new ArgumentException("Only activated abilities may have a GP cost", nameof(gpCost));
            Trigger = trigger;
            Effect = effect;
            Amount = amount;
            GpCost = gpCost;
        }
    }
}
