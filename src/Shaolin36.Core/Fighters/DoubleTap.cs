using System;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// Turns held directions into quick steps: pressing the same one of the four directions twice, with the
    /// second press at most <see cref="WindowFrames"/> ticks after the first, asks for a quick step that way.
    /// Holding a direction is one press; you have to let go (or change direction) and press again. Call
    /// <see cref="Update"/> once per 60 Hz tick with the direction held on that tick. Deterministic: the same
    /// held directions always give the same steps.
    /// </summary>
    public sealed class DoubleTap
    {
        private StepDirection _held;
        private StepDirection _lastPress;
        private int _sincePress;

        public DoubleTap(int windowFrames)
        {
            if (windowFrames < 1) throw new ArgumentOutOfRangeException(nameof(windowFrames), windowFrames, "the window is at least 1 frame");
            WindowFrames = windowFrames;
        }

        /// <summary>The most ticks from the first press to the second that still counts as a double tap.</summary>
        public int WindowFrames { get; }

        /// <summary>
        /// Feed this tick's held direction (<see cref="StepDirection.None"/> for none, or a diagonal). Returns the
        /// direction to quick-step on this tick, or <see cref="StepDirection.None"/>.
        /// </summary>
        public StepDirection Update(StepDirection held)
        {
            var step = StepDirection.None;
            if (held != StepDirection.None && held != _held)
            {
                if (held == _lastPress && _sincePress <= WindowFrames)
                {
                    step = held;
                    _lastPress = StepDirection.None; // a third tap starts a new pair
                }
                else
                {
                    _lastPress = held;
                    _sincePress = 0;
                }
            }
            if (_sincePress <= WindowFrames) _sincePress++;
            _held = held;
            return step;
        }

        /// <summary>Forget any half-finished double tap (for a new duel).</summary>
        public void Reset()
        {
            _held = StepDirection.None;
            _lastPress = StepDirection.None;
            _sincePress = 0;
        }

        /// <summary>
        /// Which of the four directions a walk input is held toward: the axis pushed further, if pushed at
        /// least <paramref name="threshold"/> (out of 100). A diagonal (both equal) or a light push counts as none.
        /// </summary>
        public static StepDirection Cardinal(MoveInput walk, int threshold = 50)
        {
            int f = walk.Forward, r = walk.Right;
            int af = Math.Abs(f), ar = Math.Abs(r);
            if (af == ar || Math.Max(af, ar) < threshold) return StepDirection.None;
            if (af > ar) return f > 0 ? StepDirection.Forward : StepDirection.Back;
            return r > 0 ? StepDirection.Right : StepDirection.Left;
        }
    }
}
