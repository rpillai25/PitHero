using System;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// A <see cref="System.Random"/> whose entire state is four 32-bit words that can be read back and
    /// restored (xoshiro128** seeded through SplitMix64). Installed as <c>Nez.Random.RNG</c> for the
    /// simulation so every gameplay roll is reproducible from a seed, and used for the other named
    /// streams in <see cref="GameRandom"/>. Allocation-free after construction.
    /// </summary>
    public sealed class SeedableRandom : System.Random
    {
        private uint _s0, _s1, _s2, _s3;

        /// <summary>Creates a generator seeded from <paramref name="seed"/>.</summary>
        public SeedableRandom(int seed) : base(0)
        {
            Reseed(seed);
        }

        /// <summary>Resets the generator to the canonical state for <paramref name="seed"/>.</summary>
        public void Reseed(int seed)
        {
            // SplitMix64 expands the 32-bit seed into four well-mixed, never-all-zero words
            ulong x = (uint)seed ^ 0x9E3779B97F4A7C15UL;
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            _s2 = SplitMix(ref x);
            _s3 = SplitMix(ref x);
            if ((_s0 | _s1 | _s2 | _s3) == 0)
                _s0 = 1;
        }

        /// <summary>Reads the full generator state.</summary>
        public void GetState(out uint s0, out uint s1, out uint s2, out uint s3)
        {
            s0 = _s0; s1 = _s1; s2 = _s2; s3 = _s3;
        }

        /// <summary>Restores a state previously read with <see cref="GetState"/>.</summary>
        public void SetState(uint s0, uint s1, uint s2, uint s3)
        {
            _s0 = s0; _s1 = s1; _s2 = s2; _s3 = s3;
            if ((_s0 | _s1 | _s2 | _s3) == 0)
                _s0 = 1;
        }

        private static uint SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (uint)(z >> 32);
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        /// <summary>Next raw 32-bit output of the generator.</summary>
        public uint NextUInt32()
        {
            uint result = RotateLeft(_s1 * 5u, 7) * 9u;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 11);
            return result;
        }

        /// <summary>Non-negative int in [0, int.MaxValue).</summary>
        public override int Next()
        {
            int v = (int)(NextUInt32() >> 1);
            return v == int.MaxValue ? 0 : v;
        }

        /// <summary>Int in [0, maxValue). Returns 0 when maxValue is not positive.</summary>
        public override int Next(int maxValue)
        {
            if (maxValue <= 0)
                return 0;
            return (int)(((ulong)NextUInt32() * (ulong)maxValue) >> 32);
        }

        /// <summary>Int in [minValue, maxValue). Returns minValue when the range is empty.</summary>
        public override int Next(int minValue, int maxValue)
        {
            long range = (long)maxValue - minValue;
            if (range <= 0)
                return minValue;
            return (int)(minValue + (long)(((ulong)NextUInt32() * (ulong)range) >> 32));
        }

        /// <summary>Double in [0, 1) with 53 bits of precision (consumes two raw outputs).</summary>
        public override double NextDouble()
        {
            ulong hi = NextUInt32();
            ulong lo = NextUInt32();
            return ((hi << 21) ^ (lo >> 11)) * (1.0 / 9007199254740992.0);
        }

        /// <inheritdoc/>
        protected override double Sample()
        {
            return NextDouble();
        }

        /// <summary>Float in [0, 1) with 24 bits of precision (one raw output).</summary>
        public override float NextSingle()
        {
            return (NextUInt32() >> 8) * (1f / 16777216f);
        }

        /// <summary>Non-negative long in [0, long.MaxValue).</summary>
        public override long NextInt64()
        {
            ulong hi = NextUInt32();
            ulong lo = NextUInt32();
            long v = (long)(((hi << 32) | lo) >> 1);
            return v == long.MaxValue ? 0 : v;
        }

        /// <summary>Long in [0, maxValue). Returns 0 when maxValue is not positive.</summary>
        public override long NextInt64(long maxValue)
        {
            if (maxValue <= 0)
                return 0;
            return (long)(NextDouble() * maxValue);
        }

        /// <summary>Long in [minValue, maxValue). Returns minValue when the range is empty.</summary>
        public override long NextInt64(long minValue, long maxValue)
        {
            if (maxValue <= minValue)
                return minValue;
            return minValue + (long)(NextDouble() * (maxValue - minValue));
        }

        /// <summary>Fills the buffer with random bytes.</summary>
        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null)
                return;
            NextBytes(buffer.AsSpan());
        }

        /// <summary>Fills the span with random bytes.</summary>
        public override void NextBytes(Span<byte> buffer)
        {
            int i = 0;
            while (i < buffer.Length)
            {
                uint v = NextUInt32();
                for (int b = 0; b < 4 && i < buffer.Length; b++, i++)
                {
                    buffer[i] = (byte)(v & 0xFF);
                    v >>= 8;
                }
            }
        }
    }
}
