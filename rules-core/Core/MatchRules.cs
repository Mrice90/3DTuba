// Port of game-core/src/main/java/com/infiniteconquest/core/MatchRules.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Tunable match parameters. Mirrors the Java record, including the
    /// compact-constructor validation (negative values rejected).
    /// </summary>
    public record MatchRules
    {
        public int InitialHandSize { get; init; }
        public int StartingGp { get; init; }
        public int SecondPlayerStartingGp { get; init; }
        public int CardsDrawnAtTurnStart { get; init; }
        public BoardGeometry Geometry { get; init; }

        public MatchRules(int initialHandSize, int startingGp, int secondPlayerStartingGp,
                          int cardsDrawnAtTurnStart, BoardGeometry geometry)
        {
            // Java also requireNonNull(geometry); enums cannot be null in C#, check omitted.
            if (initialHandSize < 0) throw new ArgumentException("Initial hand size cannot be negative");
            if (startingGp < 0 || secondPlayerStartingGp < 0 || cardsDrawnAtTurnStart < 0)
                throw new ArgumentException("Rule values cannot be negative");
            InitialHandSize = initialHandSize;
            StartingGp = startingGp;
            SecondPlayerStartingGp = secondPlayerStartingGp;
            CardsDrawnAtTurnStart = cardsDrawnAtTurnStart;
            Geometry = geometry;
        }

        public MatchRules(int hand, int gp, int secondGp, int draw)
            : this(hand, gp, secondGp, draw, BoardGeometry.SQUARE) { }

        public static MatchRules Current() => new MatchRules(5, 0, 1, 1);
        public static MatchRules Hex() => new MatchRules(5, 0, 1, 1, BoardGeometry.HEX);

        public int InitialHandSizeFor(bool startsSecond) => InitialHandSize + (startsSecond ? 1 : 0);
    }
}
