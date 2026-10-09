// Port of game-core/src/main/java/com/infiniteconquest/core/SpellEffect.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java record SpellEffect(SpellEffectType type, int amount, SpellTarget target),
    /// including the compact-constructor validation. (Java's requireNonNull on the enum
    /// components is vacuous in C# — enums are non-nullable value types.)
    /// </summary>
    public record SpellEffect
    {
        public SpellEffectType Type { get; }
        public int Amount { get; }
        public SpellTarget Target { get; }

        public SpellEffect(SpellEffectType type, int amount, SpellTarget target)
        {
            if (amount < 1) throw new ArgumentException("Spell effect amount must be positive", nameof(amount));
            Type = type;
            Amount = amount;
            Target = target;
        }
    }
}
