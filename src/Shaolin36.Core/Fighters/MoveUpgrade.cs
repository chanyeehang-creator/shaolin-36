using System;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// A run's power-ups on one move (Chan's demo run): how many times its damage went up and its stamina cost went down,
    /// and the percentages that come out of them. Immutable: each level up makes a new one.
    /// </summary>
    public sealed class MoveUpgrade
    {
        /// <summary>No upgrades: 100% damage, 100% stamina.</summary>
        public static readonly MoveUpgrade None = new MoveUpgrade(0, 0, 100, 100);

        public MoveUpgrade(int damageLevels, int staminaLevels, int damagePct, int staminaPct)
        {
            if (damageLevels < 0) throw new ArgumentOutOfRangeException(nameof(damageLevels));
            if (staminaLevels < 0) throw new ArgumentOutOfRangeException(nameof(staminaLevels));
            if (damagePct < 0) throw new ArgumentOutOfRangeException(nameof(damagePct));
            if (staminaPct < 0) throw new ArgumentOutOfRangeException(nameof(staminaPct));
            DamageLevels = damageLevels;
            StaminaLevels = staminaLevels;
            DamagePct = damagePct;
            StaminaPct = staminaPct;
        }

        /// <summary>How many "deal more damage" power-ups the move has had.</summary>
        public int DamageLevels { get; }

        /// <summary>How many "reduce stamina cost" power-ups the move has had.</summary>
        public int StaminaLevels { get; }

        /// <summary>Both kinds together.</summary>
        public int Levels => DamageLevels + StaminaLevels;

        /// <summary>Damage of every hit of the move's string, as a percentage (115 = 1.15x).</summary>
        public int DamagePct { get; }

        /// <summary>Stamina cost of every hit of the move's string, as a percentage (85 = 15% cheaper).</summary>
        public int StaminaPct { get; }
    }
}
