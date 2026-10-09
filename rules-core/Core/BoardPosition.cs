// Port of game-core/src/main/java/com/infiniteconquest/core/BoardPosition.java @ Desolate-Tuba e0e565b
using System;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Immutable battlefield coordinate. Bounds are validated on construction,
    /// mirroring the Java record's compact constructor.
    /// </summary>
    public record BoardPosition
    {
        public const int Width = 4;
        public const int Height = 6;
        public const int PlotHeight = 3;

        public int X { get; }
        public int Y { get; }

        public BoardPosition(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentException($"Position outside 4x6 battlefield: {x},{y}");
            X = x;
            Y = y;
        }

        /// <summary>Diagonal and orthogonal steps each cost one space.</summary>
        public int DistanceTo(BoardPosition other) =>
            Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public bool AdjacentTo(BoardPosition other) => DistanceTo(other) == 1;

        public bool IsOnPlayerSide(int playerId)
        {
            if (playerId == 0) return Y < PlotHeight;
            if (playerId == 1) return Y >= PlotHeight;
            throw new ArgumentException("Player ID must be 0 or 1");
        }

        /// <summary>True when the position sits on the opponent's home plot.</summary>
        public bool IsOnEnemySide(int playerId)
        {
            if (playerId == 0) return Y >= PlotHeight;
            if (playerId == 1) return Y < PlotHeight;
            throw new ArgumentException("Player ID must be 0 or 1");
        }
    }
}
