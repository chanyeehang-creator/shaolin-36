namespace Shaolin36.Core.Brains
{
    /// <summary>
    /// A tiny seeded random number source (xorshift32) for opponent brains. The same seed always
    /// gives the same rolls, on every machine, so a fight with a brain in it is still deterministic
    /// and can be replayed or tested exactly. Never seeded from the clock.
    /// </summary>
    public sealed class Dice
    {
        private uint _state;

        public Dice(int seed)
        {
            // Spread the seed so nearby seeds don't start with similar rolls; xorshift can't start on 0.
            uint s = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            _state = s == 0 ? 0x6D2B79F5u : s;
        }

        private uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>A whole number from 0 to <paramref name="count"/> - 1.</summary>
        public int Below(int count) => count <= 1 ? 0 : (int)(Next() % (uint)count);

        /// <summary>True <paramref name="percent"/> times in 100 (0 never, 100 always).</summary>
        public bool Chance(int percent) => percent >= 100 || percent > 0 && Below(100) < percent;
    }
}
