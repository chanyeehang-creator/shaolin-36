using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// What technique sits on each of the 8 slots. Any technique can go on any slot,
    /// whatever its category, and a slot can be empty. The same technique on two slots
    /// is allowed. In a run, unlocked techniques are what the player picks from.
    /// </summary>
    public sealed class Loadout
    {
        private readonly Technique?[] _slots = new Technique?[Slots.Count];

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

        private static int Index(Slot slot)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            return (int)slot;
        }
    }
}
