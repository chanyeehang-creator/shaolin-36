using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Combat
{
    /// <summary>What a fighter brings into a duel: max HP and the HP it has left, max stamina, max Qi and the Qi it has.</summary>
    public sealed class FighterStats
    {
        public FighterStats(int maxHp, int hp, int maxStamina, int maxQi, int qi)
        {
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "a fighter has at least 1 HP");
            if (hp < 1 || hp > maxHp) throw new ArgumentOutOfRangeException(nameof(hp), hp, $"HP must be between 1 and max HP ({maxHp})");
            if (maxStamina < 1) throw new ArgumentOutOfRangeException(nameof(maxStamina), maxStamina, "max stamina is at least 1");
            if (maxQi < 0) throw new ArgumentOutOfRangeException(nameof(maxQi), maxQi, "max Qi cannot be negative");
            if (qi < 0 || qi > maxQi) throw new ArgumentOutOfRangeException(nameof(qi), qi, $"Qi must be between 0 and max Qi ({maxQi})");
            MaxHp = maxHp;
            Hp = hp;
            MaxStamina = maxStamina;
            MaxQi = maxQi;
            Qi = qi;
        }

        /// <summary>The rules sheet's hp_max, stamina_max and qi_max, at full HP, with <paramref name="qi"/> (null: qi_start).</summary>
        public static FighterStats FromRules(Rules rules, int? qi = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new FighterStats(rules.HpMax, rules.HpMax, rules.StaminaMax, rules.QiMax, qi ?? rules.QiStart);
        }

        public int MaxHp { get; }
        public int Hp { get; }
        public int MaxStamina { get; }
        public int MaxQi { get; }
        public int Qi { get; }
    }
}
