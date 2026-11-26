using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// The stamina numbers one fighter plays by (from the rules sheet). Stamina is whole points;
    /// regen rates are points per second, paid out a tick at a time with the leftover fraction
    /// carried in sixtieths, so the same fight always gives the same stamina.
    /// </summary>
    public sealed class StaminaRules
    {
        public StaminaRules(int max, int regenPerSecond, int regenStillPerSecond, int regenDelayFrames,
            int exhaustedUntil, int exhaustedDamagePct, int exhaustedWalkPct)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), max, "max stamina is at least 1");
            if (regenPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(regenPerSecond), regenPerSecond, "regen cannot be negative");
            if (regenStillPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(regenStillPerSecond), regenStillPerSecond, "regen cannot be negative");
            if (regenDelayFrames < 0) throw new ArgumentOutOfRangeException(nameof(regenDelayFrames), regenDelayFrames, "the regen delay cannot be negative");
            if (exhaustedUntil < 0 || exhaustedUntil > max) throw new ArgumentOutOfRangeException(nameof(exhaustedUntil), exhaustedUntil, "must be between 0 and max stamina");
            if (exhaustedDamagePct < 100) throw new ArgumentOutOfRangeException(nameof(exhaustedDamagePct), exhaustedDamagePct, "must be 100 or more");
            if (exhaustedWalkPct < 0 || exhaustedWalkPct > 100) throw new ArgumentOutOfRangeException(nameof(exhaustedWalkPct), exhaustedWalkPct, "must be between 0 and 100");
            Max = max;
            RegenPerSecond = regenPerSecond;
            RegenStillPerSecond = regenStillPerSecond;
            RegenDelayFrames = regenDelayFrames;
            ExhaustedUntil = exhaustedUntil;
            ExhaustedDamagePct = exhaustedDamagePct;
            ExhaustedWalkPct = exhaustedWalkPct;
        }

        /// <summary>The rules sheet's stamina_max, stamina_regen, stamina_regen_still, stamina_regen_delay and exhausted_* rows.</summary>
        public static StaminaRules FromRules(Rules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new StaminaRules(rules.StaminaMax, rules.StaminaRegen, rules.StaminaRegenStill, rules.StaminaRegenDelay,
                rules.ExhaustedUntil, rules.ExhaustedDamagePct, rules.ExhaustedWalkPct);
        }

        /// <summary>The same numbers with a different max stamina (a run's own max). exhausted_until stays, capped at the new max.</summary>
        public StaminaRules WithMax(int max) =>
            new StaminaRules(max, RegenPerSecond, RegenStillPerSecond, RegenDelayFrames, Math.Min(ExhaustedUntil, max), ExhaustedDamagePct,
                ExhaustedWalkPct);

        /// <summary>Starting and max stamina.</summary>
        public int Max { get; }

        /// <summary>Points per second while walking or reeling (hit, staggered, recoiling).</summary>
        public int RegenPerSecond { get; }

        /// <summary>Points per second while idle and standing still.</summary>
        public int RegenStillPerSecond { get; }

        /// <summary>Ticks with no move and no stamina lost before regen starts.</summary>
        public int RegenDelayFrames { get; }

        /// <summary>At 0 stamina a fighter is exhausted until stamina is back up to this.</summary>
        public int ExhaustedUntil { get; }

        /// <summary>Damage an exhausted fighter takes, as a percentage of normal.</summary>
        public int ExhaustedDamagePct { get; }

        /// <summary>Walking speed while exhausted, as a percentage of normal.</summary>
        public int ExhaustedWalkPct { get; }
    }
}
