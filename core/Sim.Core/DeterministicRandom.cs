using System;

namespace LToLocal.Sim
{
    /// <summary>
    /// Small, seedable random number generator (xorshift64*) with state that can be
    /// saved in a snapshot and restored for replays. We don't use System.Random because
    /// its algorithm is not guaranteed to be the same across .NET runtimes (Unity/IL2CPP vs .NET).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            // Zero is a fixed point for xorshift; remap it.
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>Raw generator state, for snapshots.</summary>
        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? 0x9E3779B97F4A7C15UL : value;
        }

        public ulong NextUInt64()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform double in [min, max).</summary>
        public double Range(double min, double max)
        {
            if (max < min) throw new ArgumentException("max must be >= min");
            return min + (max - min) * NextDouble();
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentException("maxExclusive must be > minInclusive");
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextUInt64() % span));
        }
    }
}
