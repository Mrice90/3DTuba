using System;
using System.Collections.Generic;
namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>Java Random's 48-bit stream, with exact bounded rejection sampling.</summary>
    public sealed class SeededRng
    {
        private const long Multiplier = 0x5DEECE66DL;
        private const long Mask = (1L << 48) - 1;
        private long _state;
        public long State => _state;
        public SeededRng(long seed) { _state = (seed ^ Multiplier) & Mask; }
        private SeededRng(long state, bool raw) { _state = state; }
        public SeededRng Copy() => new SeededRng(_state, true);
        private int NextBits(int bits)
        {
            _state = unchecked(_state * Multiplier + 11) & Mask;
            return (int)((ulong)_state >> (48 - bits));
        }
        public int Next(int bound)
        {
            if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));
            if ((bound & -bound) == bound) return (int)((bound * (long)NextBits(31)) >> 31);
            int bits, value;
            do { bits = NextBits(31); value = bits % bound; }
            while (unchecked(bits - value + (bound - 1)) < 0);
            return value;
        }
        public int Next(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            int range = unchecked(maxExclusive - minInclusive);
            if (range > 0) return unchecked(minInclusive + Next(range));
            int value;
            do { value = NextBits(32); } while (value < minInclusive || value >= maxExclusive);
            return value;
        }
        public double NextDouble() => (((long)NextBits(26) << 27) + NextBits(27)) / (double)(1L << 53);
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T value = list[i]; list[i] = list[j]; list[j] = value;
            }
        }
    }
}
