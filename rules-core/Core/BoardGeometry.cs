// Port of game-core/src/main/java/com/infiniteconquest/core/BoardGeometry.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Rules geometry, independent of rendering. HEX uses odd-row offset coordinates.
    /// Java enum → C# enum (UPPER_SNAKE kept for wire parity); the enum's behavior
    /// lives in extension methods so call sites keep their Java shape
    /// (e.g. geometry.Distance(a, b)).
    /// </summary>
    public enum BoardGeometry
    {
        SQUARE,
        HEX
    }

    public static class BoardGeometryExtensions
    {
        public static int Distance(this BoardGeometry geometry, BoardPosition a, BoardPosition b)
        {
            if (geometry == BoardGeometry.SQUARE) return a.DistanceTo(b);
            // Integer division truncates toward zero in both Java and C#.
            int aq = a.X - (a.Y - (a.Y & 1)) / 2;
            int bq = b.X - (b.Y - (b.Y & 1)) / 2;
            return Math.Max(Math.Abs(aq - bq), Math.Max(Math.Abs(a.Y - b.Y),
                Math.Abs(aq + a.Y - bq - b.Y)));
        }

        public static bool Adjacent(this BoardGeometry geometry, BoardPosition a, BoardPosition b) =>
            geometry.Distance(a, b) == 1;

        public static IReadOnlyList<BoardPosition> Neighbors(this BoardGeometry geometry, BoardPosition origin)
        {
            var result = new List<BoardPosition>();
            // Row-major scan order mirrors the Java loops (y outer, x inner).
            for (int y = 0; y < BoardPosition.Height; y++)
                for (int x = 0; x < BoardPosition.Width; x++)
                {
                    var p = new BoardPosition(x, y);
                    if (geometry.Adjacent(origin, p)) result.Add(p);
                }
            return result;
        }

        /// <summary>
        /// Two symmetric cube-coordinate traces resolve exact shared-edge ambiguity.
        /// </summary>
        public static IReadOnlyList<BoardPosition> HexTrace(this BoardGeometry geometry, BoardPosition a, BoardPosition b, double nudge)
        {
            // Canonical direction makes rounding identical when sight is queried in reverse.
            if (a.Y * 4 + a.X > b.Y * 4 + b.X) return geometry.HexTrace(b, a, nudge);
            int steps = geometry.Distance(a, b);
            var result = new List<BoardPosition>();
            double aq = a.X - (a.Y - (a.Y & 1)) / 2.0;
            double bq = b.X - (b.Y - (b.Y & 1)) / 2.0;
            for (int i = 1; i < steps; i++)
            {
                double t = (double)i / steps;
                double q = aq + (bq - aq) * t + nudge;
                double r = a.Y + (b.Y - a.Y) * t + nudge;
                double s = -q - r;
                // Java Math.round(double) is specified as floor(x + 0.5); C# Math.Round
                // defaults to banker's rounding, so the Java semantics are spelled out
                // literally here. This is load-bearing for trace parity.
                int rq = (int)Math.Floor(q + 0.5), rr = (int)Math.Floor(r + 0.5), rs = (int)Math.Floor(s + 0.5);
                double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
                if (dq > dr && dq > ds) rq = -rr - rs;
                else if (dr > ds) rr = -rq - rs;
                int x = rq + (rr - (rr & 1)) / 2;
                if (x >= 0 && x < BoardPosition.Width && rr >= 0 && rr < BoardPosition.Height)
                    result.Add(new BoardPosition(x, rr));
            }
            return result;
        }
    }
}
