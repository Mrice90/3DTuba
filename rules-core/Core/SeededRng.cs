// Port of: deterministic RNG utility (no direct Java counterpart; see CONVENTIONS.md §4).
// All game randomness flows through this type, owned by GameState.
using System;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Seeded RNG for all game-logic randomness. Wraps System.Random.
    /// PARITY NOTE: java.util.Random and System.Random produce different streams
    /// for the same seed. If AI-084 requires seed-for-seed parity with the Java
    /// engine, replace the internals with a ported 48-bit Java-compatible LCG.
    /// Do not change this silently — it changes every golden hash.
    /// </summary>
    public sealed class SeededRng
    {
        private readonly Random _inner;

        public SeededRng(long seed)
        {
            // System.Random takes int; fold the long deterministically.
            _inner = new Random(unchecked((int)(seed ^ (seed >>> 32))));
        }

        public int Next(int bound)
        {
            if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));
            return _inner.Next(bound);
        }

        public int Next(int minInclusive, int maxExclusive) => _inner.Next(minInclusive, maxExclusive);

        public double NextDouble() => _inner.NextDouble();

        /// <summary>Fisher-Yates shuffle, deterministic given the stream.</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
