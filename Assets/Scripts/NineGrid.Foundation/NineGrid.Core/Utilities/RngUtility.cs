using System;
using QFramework;

namespace NineGrid.Core.Utilities
{
    public interface IRngUtility : IUtility
    {
        ulong Seed { get; }
        RngState State { get; }
        void SetSeed(ulong seed);
        uint NextUInt();
        int Range(int minInclusive, int maxExclusive);
        float Value01();
        RngState CaptureState();
        void RestoreState(RngState state);
    }

    public struct RngState
    {
        public readonly uint X;
        public readonly uint Y;
        public readonly uint Z;
        public readonly uint W;
        public readonly ulong Step;

        public RngState(uint x, uint y, uint z, uint w, ulong step)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            Step = step;
        }

        public override string ToString()
        {
            return X + ":" + Y + ":" + Z + ":" + W + ":" + Step;
        }
    }

    public sealed class DeterministicRngUtility : IRngUtility
    {
        public ulong Seed { get; private set; }
        public RngState State { get; private set; }

        public DeterministicRngUtility()
            : this(1UL)
        {
        }

        public DeterministicRngUtility(ulong seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(ulong seed)
        {
            Seed = seed;

            var x = ToNonZeroUInt(Mix(seed + 0x9E3779B97F4A7C15UL));
            var y = ToNonZeroUInt(Mix(seed + 0xBF58476D1CE4E5B9UL));
            var z = ToNonZeroUInt(Mix(seed + 0x94D049BB133111EBUL));
            var w = ToNonZeroUInt(Mix(seed + 0xD1B54A32D192ED03UL));

            State = new RngState(x, y, z, w, 0UL);
        }

        public uint NextUInt()
        {
            var state = State;
            var t = state.X ^ (state.X << 11);
            var next = state.W ^ (state.W >> 19) ^ t ^ (t >> 8);
            State = new RngState(state.Y, state.Z, state.W, next, state.Step + 1UL);
            return next;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException("maxExclusive", "maxExclusive must be greater than minInclusive.");
            }

            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public float Value01()
        {
            return NextUInt() / (float)uint.MaxValue;
        }

        public RngState CaptureState()
        {
            return State;
        }

        public void RestoreState(RngState state)
        {
            if (state.X == 0U && state.Y == 0U && state.Z == 0U && state.W == 0U)
            {
                throw new ArgumentException("RNG state cannot be all zero.", "state");
            }

            State = state;
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        private static uint ToNonZeroUInt(ulong value)
        {
            var result = (uint)(value ^ (value >> 32));
            return result == 0U ? 0xA341316CU : result;
        }
    }
}
