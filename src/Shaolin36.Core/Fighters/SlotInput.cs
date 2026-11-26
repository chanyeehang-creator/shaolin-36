using System;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// One tick of input: which slots were pressed this tick. A press is the frame the
    /// button goes down, not every frame it is held. Stored as 8 bits so a whole fight's
    /// input can be recorded and replayed exactly. <see cref="HeldBits"/> says which buttons are
    /// down on this tick (only a held move such as the 硬气功 guard looks at it); a press counts as held.
    /// </summary>
    public readonly struct SlotInput : IEquatable<SlotInput>
    {
        public static readonly SlotInput None = new SlotInput(0);

        public SlotInput(byte bits) : this(bits, 0) { }

        public SlotInput(byte bits, byte heldBits)
        {
            Bits = bits;
            HeldBits = heldBits;
        }

        public byte Bits { get; }

        /// <summary>Buttons held down on this tick (presses aside).</summary>
        public byte HeldBits { get; }

        /// <summary>This tick's presses with <paramref name="slot"/> held down as well.</summary>
        public SlotInput Holding(Slot slot)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            return new SlotInput(Bits, (byte)(HeldBits | (1 << (int)slot)));
        }

        /// <summary>Just <paramref name="slot"/> held down, nothing pressed.</summary>
        public static SlotInput Hold(Slot slot) => None.Holding(slot);

        /// <summary>True if <paramref name="slot"/> is down on this tick: pressed or held.</summary>
        public bool IsHeld(Slot slot) => Slots.IsValid(slot) && ((Bits | HeldBits) & (1 << (int)slot)) != 0;

        public static SlotInput Press(Slot slot) => None.With(slot);

        public SlotInput With(Slot slot)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            return new SlotInput((byte)(Bits | (1 << (int)slot)), HeldBits);
        }

        public bool IsPressed(Slot slot) => Slots.IsValid(slot) && (Bits & (1 << (int)slot)) != 0;

        public bool Any => Bits != 0;

        public bool Equals(SlotInput other) => Bits == other.Bits && HeldBits == other.HeldBits;
        public override bool Equals(object? obj) => obj is SlotInput other && Equals(other);
        public override int GetHashCode() => Bits | HeldBits << 8;
        public override string ToString() => Any ? Convert.ToString(Bits, 2).PadLeft(Slots.Count, '0') : "none";
    }
}
