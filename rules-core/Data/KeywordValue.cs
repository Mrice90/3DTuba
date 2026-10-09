// Port of game-core/src/main/java/com/infiniteconquest/core/KeywordValue.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Range in board spaces and the strength of a development keyword.
    /// Mirrors Java record KeywordValue(int range, int amount).
    /// </summary>
    public record KeywordValue
    {
        public int Range { get; }
        public int Amount { get; }

        public KeywordValue(int range, int amount)
        {
            if (range < 0 || range > 12 || amount < 1 || amount > 100)
                throw new ArgumentException("Keyword range must be 0–12 and amount 1–100");
            Range = range;
            Amount = amount;
        }
    }
}
