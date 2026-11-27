using System;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// Which foot a pose lifts off the floor, for kicks, knee raises and one-leg stances. Standing poses keep both
    /// feet on the floor (the mannequin levels them); a leg swung far enough from straight down leaves the floor instead:
    /// it starts to lift at <see cref="StartDegrees"/> and is fully up from <see cref="FullDegrees"/>, so a blend into a
    /// kick lifts the foot smoothly. Only one foot can be up: when both legs are raised (a very deep horse stance) the
    /// pose stands on both. Look only, like every pose.
    /// </summary>
    public static class FootLift
    {
        /// <summary>A leg swung this far (forward, back or out to the side) starts to leave the floor.</summary>
        public const int StartDegrees = 75;

        /// <summary>A leg swung this far is fully off the floor: the thigh is about level with the hip. A deep horse stance stays below it.</summary>
        public const int FullDegrees = 85;

        /// <summary>How far a leg swings away from straight down: the larger of its pitch and its roll.</summary>
        public static int Raise(Pose p, Joint leg) => Math.Max(Math.Abs(p.Pitch(leg)), Math.Abs(p.Roll(leg)));

        /// <summary>0 (standing) to 1000 (off the floor) for one leg's raise, ignoring the other leg.</summary>
        public static int OfRaise(int degrees)
        {
            if (degrees <= StartDegrees) return 0;
            if (degrees >= FullDegrees) return 1000;
            return (degrees - StartDegrees) * 1000 / (FullDegrees - StartDegrees);
        }

        /// <summary>
        /// How far the pose lifts a foot, 0 (both feet stand) to 1000 (one foot fully off the floor), and which one:
        /// the leg raised more. Raising the other leg as well takes it back down (both raised the same: both stand).
        /// </summary>
        public static int Of(Pose p, out bool right)
        {
            int r = OfRaise(Raise(p, Joint.LegR)), l = OfRaise(Raise(p, Joint.LegL));
            right = r > l;
            return Math.Abs(r - l);
        }
    }
}
