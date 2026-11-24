namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// The 8 button slots. A slot is just a place to put a technique; the rules never
    /// ask "was parry pressed", only "was slot N pressed", so any technique the player
    /// unlocks can sit on any button. The names follow the PlayStation pad; the Godot
    /// side maps keyboard keys and other pads onto the same 8 slots.
    /// </summary>
    public enum Slot
    {
        Square = 0,
        Triangle = 1,
        Circle = 2,
        Cross = 3,
        L1 = 4,
        L2 = 5,
        R1 = 6,
        R2 = 7,
    }

    public static class Slots
    {
        public const int Count = 8;

        /// <summary>Every slot, in a fixed order. When two slots are pressed on the same tick, the earlier one wins.</summary>
        public static readonly Slot[] All =
        {
            Slot.Square, Slot.Triangle, Slot.Circle, Slot.Cross, Slot.L1, Slot.L2, Slot.R1, Slot.R2,
        };

        /// <summary>The label shown on screen: □ △ ○ ✕ L1 L2 R1 R2.</summary>
        public static string Label(Slot slot)
        {
            switch (slot)
            {
                case Slot.Square: return "□";
                case Slot.Triangle: return "△";
                case Slot.Circle: return "○";
                case Slot.Cross: return "✕";
                default: return slot.ToString();
            }
        }

        public static bool IsValid(Slot slot) => slot >= Slot.Square && slot <= Slot.R2;
    }
}
