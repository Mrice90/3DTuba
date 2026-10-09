// Port of game-cli/src/main/java/com/infiniteconquest/cli/BotDifficulty.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Bot
{
    /// <summary>
    /// Bot skill levels, themed as the Greek heroic journey. Every level plays the
    /// same game with the same legal actions; they differ only in how actions are
    /// chosen.
    /// </summary>
    public enum BotDifficulty
    {
        /// <summary>Learning the ropes: clears the board like a cautious novice, rarely goes for the win.</summary>
        MORTAL,
        /// <summary>The classic challenge: the original heuristic tactician, unchanged.</summary>
        HERO,
        /// <summary>A ruthless tactician: simulates each candidate move a turn ahead before choosing.</summary>
        DEMIGOD
    }

    /// <summary>
    /// Display data carried on the Java enum. C# enums cannot carry fields, so the
    /// title/description travel as extension methods; the selector-facing strings
    /// are unchanged.
    /// </summary>
    public static class BotDifficultyExtensions
    {
        /// <summary>Display name shown in the difficulty selector.</summary>
        public static string Title(this BotDifficulty difficulty) => difficulty switch
        {
            BotDifficulty.MORTAL => "Mortal",
            BotDifficulty.HERO => "Hero",
            BotDifficulty.DEMIGOD => "Demigod",
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };

        /// <summary>One-line plain-language description shown under the selector.</summary>
        public static string Description(this BotDifficulty difficulty) => difficulty switch
        {
            BotDifficulty.MORTAL => "Learning the ropes — cautious, often misses the winning move.",
            BotDifficulty.HERO => "A competent tactician — the classic challenge.",
            BotDifficulty.DEMIGOD => "Ruthless tactician — thinks a move ahead.",
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };
    }
}
