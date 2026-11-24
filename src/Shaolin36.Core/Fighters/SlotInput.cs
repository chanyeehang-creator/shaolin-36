using System;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// One tick of input: which slots were pressed this tick. A press is the frame the
    /// button goes down, not every frame it is held. Stored as 8 bits so a whole fight's
    /// input can be recorded and replayed exactly.
    /// </summary>
    public readonly struct SlotInput : IEquatable<SlotInput>
    {
        public static readonly SlotInput None = new SlotInput(0);

        public SlotInput(byte bits) => Bits = bits;

        public byte Bits { get; }

        public static SlotInput Press(Slot slot) => None.With(slot);

        public SlotInput With(Slot slot)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            return new SlotInput((byte)(Bits | (1 << (int)slot)));
        }

        public bool IsPressed(Slot slot) => Slots.IsValid(slot) && (Bits & (1 << (int)slot)) != 0;

        public bool Any => Bits != 0;

        public bool Equals(SlotInput other) => Bits == other.Bits;
        public override bool Equals(object? obj) => obj is SlotInput other && Equals(other);
        public override int GetHashCode() => Bits;
        public override string ToString() => Any ? Convert.ToString(Bits, 2).PadLeft(Slots.Count, '0') : "none";
    }
}
