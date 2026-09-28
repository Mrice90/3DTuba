using System;
namespace InfiniteConquest.Proof {
    // Deliberately restricted to the two pinned AI-036 scenarios, on the HEX geometry the alpha plays
    // (MatchRules.hex(): odd-row offset coordinates, six neighbours). The AI-036 fixture's own note that
    // diagonals cost one comes from the engine's SQUARE test default and does not apply to real matches.
    public sealed class MovementState {
        public const int Width = 4, Height = 6;
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Spent { get; private set; }
        public bool EnemyStructure { get; private set; }
        public void Reset(bool blocked) { X = Y = Spent = 0; EnemyStructure = blocked; }
        public bool CanMove(int x, int y) {
            if (x < 0 || y < 0 || x >= Width || y >= Height || Spent >= 1) return false;
            return HexDistance(X, Y, x, y) == 1 && !(EnemyStructure && x == 1 && y == 0);
        }
        // Port of TubaExperiment BoardGeometry.HEX.distance.
        public static int HexDistance(int ax, int ay, int bx, int by) {
            int aq = ax - (ay - (ay & 1)) / 2, bq = bx - (by - (by & 1)) / 2;
            return Math.Max(Math.Abs(aq - bq), Math.Max(Math.Abs(ay - by), Math.Abs(aq + ay - bq - by)));
        }
        public bool TryMove(int x, int y) {
            if (!CanMove(x,y)) return false;
            X=x; Y=y; Spent++; return true;
        }
    }
}

