using System;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// One tick of movement input, relative to the lock-on: <see cref="Forward"/> is toward the other
    /// fighter and <see cref="Right"/> is to your right as you face them. Each runs from -100 to 100,
    /// the percentage the stick is pushed (keys give 100). Together they are capped at 100, so a diagonal
    /// is no faster than a straight walk, and a half-pushed stick walks at half speed.
    /// <see cref="QuickStep"/> asks for a quick step this tick (from a double tap, see <see cref="DoubleTap"/>).
    /// </summary>
    public readonly struct MoveInput : IEquatable<MoveInput>
    {
        /// <summary>A stick pushed all the way (or a key held).</summary>
        public const int Full = 100;

        public MoveInput(int forward, int right, StepDirection quickStep = StepDirection.None)
        {
            Forward = Clamp(forward);
            Right = Clamp(right);
            QuickStep = quickStep;
        }

        public static MoveInput None => default;

        /// <summary>Toward the other fighter (-100 to 100; negative is backward).</summary>
        public int Forward { get; }

        /// <summary>To your right as you face the other fighter (-100 to 100; negative is left).</summary>
        public int Right { get; }

        /// <summary>A quick step asked for on this tick, or <see cref="StepDirection.None"/>.</summary>
        public StepDirection QuickStep { get; }

        public bool IsWalking => Forward != 0 || Right != 0;

        /// <summary>A quick step this way, with no walking.</summary>
        public static MoveInput Step(StepDirection direction) => new MoveInput(0, 0, direction);

        /// <summary>This walk with a quick step added.</summary>
        public MoveInput WithQuickStep(StepDirection direction) => new MoveInput(Forward, Right, direction);

        public static MoveInput WalkForward => new MoveInput(Full, 0);
        public static MoveInput WalkBack => new MoveInput(-Full, 0);
        public static MoveInput WalkLeft => new MoveInput(0, -Full);
        public static MoveInput WalkRight => new MoveInput(0, Full);

        private static int Clamp(int v) => v < -Full ? -Full : v > Full ? Full : v;

        public bool Equals(MoveInput other) => Forward == other.Forward && Right == other.Right && QuickStep == other.QuickStep;
        public override bool Equals(object? obj) => obj is MoveInput m && Equals(m);
        public override int GetHashCode() => ((Forward * 397) ^ Right) * 31 + (int)QuickStep;
        public override string ToString() =>
            (IsWalking ? $"walk {Forward} forward, {Right} right" : "stand") + (QuickStep != StepDirection.None ? $", quick step {QuickStep}" : "");
    }
}
