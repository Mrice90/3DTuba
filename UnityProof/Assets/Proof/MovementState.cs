using System;
namespace InfiniteConquest.Proof {
    // Deliberately restricted to the two pinned AI-036 scenarios.
    public sealed class MovementState {
        public const int Width = 4, Height = 6;
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Spent { get; private set; }
        public bool EnemyStructure { get; private set; }
        public void Reset(bool blocked) { X = Y = Spent = 0; EnemyStructure = blocked; }
        public bool CanMove(int x, int y) {
            if (x < 0 || y < 0 || x >= Width || y >= Height || Spent >= 1) return false;
            int distance = Math.Max(Math.Abs(x-X), Math.Abs(y-Y));
            return distance == 1 && !(EnemyStructure && x == 1 && y == 0);
        }
        public bool TryMove(int x, int y) {
            if (!CanMove(x,y)) return false;
            X=x; Y=y; Spent++; return true;
        }
    }
}

