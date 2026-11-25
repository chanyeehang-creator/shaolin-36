using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// Knocked down and getting up (from the rules sheet). A knocked-down fighter lies on the floor for at least
    /// <see cref="MinFrames"/>, then any slot button rises (<see cref="RiseFrames"/>) and a direction held rolls away
    /// (<see cref="RollDistanceCm"/> over <see cref="RollFrames"/>). Both are invincible. After <see cref="MaxFrames"/>
    /// they rise by themselves. While exhausted they can't do either until stamina is back to exhausted_until.
    /// </summary>
    public sealed class DownRules
    {
        public DownRules(int minFrames, int maxFrames, int riseFrames, int rollFrames, int rollDistanceCm)
        {
            if (minFrames < 0) throw new ArgumentOutOfRangeException(nameof(minFrames), minFrames, "cannot be negative");
            if (maxFrames < minFrames) throw new ArgumentOutOfRangeException(nameof(maxFrames), maxFrames, "cannot be less than the minimum");
            if (riseFrames < 1) throw new ArgumentOutOfRangeException(nameof(riseFrames), riseFrames, "a rise takes at least 1 frame");
            if (rollFrames < 1) throw new ArgumentOutOfRangeException(nameof(rollFrames), rollFrames, "a roll takes at least 1 frame");
            if (rollDistanceCm < 0) throw new ArgumentOutOfRangeException(nameof(rollDistanceCm), rollDistanceCm, "cannot be negative");
            MinFrames = minFrames;
            MaxFrames = maxFrames;
            RiseFrames = riseFrames;
            RollFrames = rollFrames;
            RollDistanceCm = rollDistanceCm;
        }

        /// <summary>The rules sheet's down_min, down_max, rise_frames, roll_frames and roll_distance.</summary>
        public static DownRules FromRules(Rules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new DownRules(rules.DownMin, rules.DownMax, rules.RiseFrames, rules.RollFrames, rules.RollDistance);
        }

        /// <summary>Frames on the floor before getting up is possible.</summary>
        public int MinFrames { get; }

        /// <summary>Frames on the floor after which the fighter rises by themselves (if not exhausted).</summary>
        public int MaxFrames { get; }

        public int RiseFrames { get; }
        public int RollFrames { get; }
        public int RollDistanceCm { get; }
    }
}
