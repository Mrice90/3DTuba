// Port of game-core/src/main/java/com/infiniteconquest/core/BoardPosition.java @ Desolate-Tuba e0e565b
using System;
using System.Text.Json.Serialization;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Immutable battlefield coordinate. Bounds are validated on construction,
    /// mirroring the Java record's compact constructor.
    /// </summary>
    public record BoardPosition
    {
        public const int Width = 7;
        public const int Height = 7;
        public const int PlotHeight = 3;
        public const int Radius = 3;
        public const int CenterX = 3, CenterY = 3;
        public const int CellCount = 37;

        // Odd-row offset coordinates. The middle row is neutral territory.
        public static bool Contains(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            int q = x - (y - (y & 1)) / 2;
            int cq = CenterX - (CenterY - (CenterY & 1)) / 2;
            return Math.Max(Math.Abs(q-cq), Math.Max(Math.Abs(y-CenterY), Math.Abs(q+y-cq-CenterY))) <= Radius;
        }
        public static int HomeSide(int x, int y) => !Contains(x,y) || y == CenterY ? -1 : y < CenterY ? 0 : 1;

        [JsonPropertyName("x")] public int X { get; }
        [JsonPropertyName("y")] public int Y { get; }

        public BoardPosition(int x, int y)
        {
            if (!Contains(x, y))
                throw new ArgumentException($"Position outside {Width}x{Height} battlefield: {x},{y}");
            X = x;
            Y = y;
        }

        /// <summary>Diagonal and orthogonal steps each cost one space.</summary>
        public override string ToString() => $"BoardPosition[x={X}, y={Y}]";

        public int DistanceTo(BoardPosition other) =>
            Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public bool AdjacentTo(BoardPosition other) => DistanceTo(other) == 1;

        public bool IsOnPlayerSide(int playerId)
        {
            if (playerId == 0 || playerId == 1) return HomeSide(X, Y) == playerId;
            throw new ArgumentException("Player ID must be 0 or 1");
        }

        /// <summary>True when the position sits on the opponent's home plot.</summary>
        public bool IsOnEnemySide(int playerId)
        {
            if (playerId == 0 || playerId == 1) return HomeSide(X, Y) == 1-playerId;
            throw new ArgumentException("Player ID must be 0 or 1");
        }
    }
}
