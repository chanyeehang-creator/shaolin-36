using System;
using System.Collections.Generic;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// What technique sits on each of the 8 slots. Any technique can go on any slot,
    /// whatever its category, and a slot can be empty. The same technique on two slots
    /// is allowed. In a run, unlocked techniques are what the player picks from.
    ///
    /// The later hits of a string (罗汉拳 2 to 4) don't sit on slots: pressing the first hit's
    /// slot again in its chain window plays them, and <see cref="Book"/> is where they are found.
    /// </summary>
    public sealed class Loadout
    {
        private readonly Technique?[] _slots = new Technique?[Slots.Count];
        private readonly Dictionary<Technique, MoveUpgrade> _upgrades = new Dictionary<Technique, MoveUpgrade>();

        /// <param name="book">Where string follow-ups are looked up. Needed as soon as a technique with a next hit is chained; chaining without it throws.</param>
        public Loadout(TechniqueBook? book = null) => Book = book;

        /// <summary>Every technique the game knows, for finding the next hit of a string. May be null.</summary>
        public TechniqueBook? Book { get; }

        public Technique? this[Slot slot] => _slots[Index(slot)];

        public void Assign(Slot slot, Technique technique)
        {
            if (technique == null) throw new ArgumentNullException(nameof(technique), "use Clear to empty a slot");
            _slots[Index(slot)] = technique;
        }

        public void Clear(Slot slot) => _slots[Index(slot)] = null;

        /// <summary>Exchange what two slots hold, for example moving parry from L1 to ○.</summary>
        public void Swap(Slot a, Slot b)
        {
            int i = Index(a), j = Index(b);
            var t = _slots[i];
            _slots[i] = _slots[j];
            _slots[j] = t;
        }

        public bool IsEmpty(Slot slot) => this[slot] == null;

        /// <summary>
        /// Put <paramref name="with"/> on every slot that holds <paramref name="old"/> (the pause menu's swap between a category's
        /// two moves). False if no slot held it.
        /// </summary>
        public bool Replace(Technique old, Technique with)
        {
            if (old == null) throw new ArgumentNullException(nameof(old));
            if (with == null) throw new ArgumentNullException(nameof(with));
            bool any = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != old) continue;
                _slots[i] = with;
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Run upgrades on <paramref name="technique"/> (the move on a slot; they cover every hit of its string). They stay with the
        /// technique, whichever slot it sits on.
        /// </summary>
        public MoveUpgrade UpgradesOf(Technique technique) =>
            _upgrades.TryGetValue(technique ?? throw new ArgumentNullException(nameof(technique)), out var u) ? u : MoveUpgrade.None;

        /// <summary>Set the run upgrades on <paramref name="technique"/>.</summary>
        public void SetUpgrades(Technique technique, MoveUpgrade upgrade)
        {
            if (technique == null) throw new ArgumentNullException(nameof(technique));
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));
            _upgrades[technique] = upgrade;
        }

        /// <summary><paramref name="damage"/> from a hit of <paramref name="slotMove"/>'s string, with its damage upgrades (rounded half up).</summary>
        public int UpgradedDamage(Technique slotMove, int damage)
        {
            var u = UpgradesOf(slotMove);
            return u.DamagePct == 100 ? damage : (damage * u.DamagePct + 50) / 100;
        }

        /// <summary>
        /// Stamina a hit of <paramref name="slotMove"/>'s string costs, with its stamina upgrades (rounded half up, and never
        /// below 1 when it cost something to begin with).
        /// </summary>
        public int UpgradedStaminaCost(Technique slotMove, int cost)
        {
            var u = UpgradesOf(slotMove);
            if (u.StaminaPct == 100 || cost == 0) return cost;
            return Math.Max(1, (cost * u.StaminaPct + 50) / 100);
        }

        private static int Index(Slot slot)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            return (int)slot;
        }
    }
}
