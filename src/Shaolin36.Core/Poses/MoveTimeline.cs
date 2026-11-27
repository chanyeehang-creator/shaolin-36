using Shaolin36.Core.Data;

namespace Shaolin36.Core.Poses
{
    /// <summary>The key poses a move blends between.</summary>
    public enum PoseKey
    {
        /// <summary>Whatever the fighter showed when the move started (the stance, or the last hit of a string).</summary>
        Entry,
        WindUp,
        Strike,
        Stance,
    }

    /// <summary>Where a move's animation is on one frame: <see cref="Permille"/>/1000 of the way from one key pose to the next (already eased).</summary>
    public readonly struct PoseStep
    {
        public PoseStep(PoseKey from, PoseKey to, int permille)
        {
            From = from;
            To = to;
            Permille = permille;
        }

        public PoseKey From { get; }
        public PoseKey To { get; }
        public int Permille { get; }

        public override string ToString() => $"{From}->{To} {Permille}";
    }

    /// <summary>
    /// Times a move's key poses to its frame data, so the animation always follows the hitbox:
    /// <code>
    ///   [ startup ................ ][ active ][ recovery ........ ]
    ///   [ into wind-up ][ held     ][ strike  ][ back to stance    ]
    /// </code>
    /// The body moves into the wind-up over the first half of the startup and holds it, cocked, until the last startup
    /// frame: that held pose is the tell to read. The strike pose snaps in on the first active frame, exactly when the
    /// hit can land, and stays for every active frame (a held guard, a grab's hold). The recovery eases back to the stance.
    /// </summary>
    public static class MoveTimeline
    {
        /// <summary>Frames the body takes to reach the wind-up: half the startup, rounded up (at least 1).</summary>
        public static int WindUpReachedAfter(Technique t) => t.StartupFrames <= 1 ? 1 : (t.StartupFrames + 1) / 2;

        public static PoseStep At(Technique t, int frame)
        {
            if (frame < 0) return new PoseStep(PoseKey.Entry, PoseKey.Entry, 1000);
            if (frame < t.StartupFrames)
            {
                int reach = WindUpReachedAfter(t);
                int x = frame + 1 >= reach ? 1000 : (frame + 1) * 1000 / reach;
                return new PoseStep(PoseKey.Entry, PoseKey.WindUp, Pose.Ease(x));
            }
            if (frame < t.StartupFrames + t.ActiveFrames) return new PoseStep(PoseKey.Strike, PoseKey.Strike, 1000);
            int into = frame - t.StartupFrames - t.ActiveFrames + 1;
            int x2 = t.RecoveryFrames <= 0 || into >= t.RecoveryFrames ? 1000 : into * 1000 / t.RecoveryFrames;
            return new PoseStep(PoseKey.Strike, PoseKey.Stance, Pose.Ease(x2));
        }
    }
}
