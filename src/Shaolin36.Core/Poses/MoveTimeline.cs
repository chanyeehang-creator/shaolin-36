using System;
using System.Collections.Generic;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Poses
{
    /// <summary>The three parts of a move a key pose belongs to: the startup, the active frames and the recovery.</summary>
    public enum MovePhase
    {
        WindUp,
        Strike,
        Recovery,
    }

    /// <summary>One key pose of a move: the pose, its phase, and how far through that phase it is reached (0-100).</summary>
    public readonly struct MoveKey
    {
        public MoveKey(Pose pose, MovePhase phase, int percent)
        {
            Pose = pose;
            Phase = phase;
            Percent = percent;
        }

        public Pose Pose { get; }
        public MovePhase Phase { get; }
        public int Percent { get; }

        public override string ToString() => $"{Pose.Name} ({Phase} {Percent}%)";
    }

    /// <summary>
    /// Times a move's key poses to its frame data, so the animation always follows the hitbox:
    /// <code>
    ///   [ startup ................ ][ active ][ recovery ........ ]
    ///   [ into wind-up ][ held     ][ strike  ][ back to stance    ]
    /// </code>
    /// With one wind-up key (".windup", reached halfway through the startup) the body moves into it and holds it, cocked,
    /// until the last startup frame: that held pose is the tell to read. A wind-up whose last key is at 100% ("windup@100")
    /// never holds: it keeps moving and reaches that key on the last startup frame. More keys (".windup@30", ".windup@70")
    /// are passed through in order. The strike pose snaps in on the first active frame, exactly when the hit can land, and
    /// is held through the active frames, unless more strike keys (".strike@100") carry it on through them. The recovery
    /// passes through any recovery keys (".recovery@50") and settles into the stance on its last frame. A key that repeats
    /// the one before it holds still between the two (a recovery key repeating the last strike pose holds the finish).
    /// The body only slows where it stops: easing into a held pose, out of one, and into the stance; through key poses it
    /// keeps its speed.
    /// </summary>
    public sealed class MoveTimeline
    {
        private readonly List<int> _frames = new List<int>();
        private readonly List<Pose?> _poses = new List<Pose?>(); // null = the entry pose
        private readonly List<bool> _rest = new List<bool>();

        private MoveTimeline() { }

        /// <summary>Frames the body takes to reach a plain wind-up (".windup", 50%): half the startup, rounded up (at least 1).</summary>
        public static int WindUpReachedAfter(Technique t) => t.StartupFrames <= 1 ? 1 : (t.StartupFrames + 1) / 2;

        /// <summary>True if the wind-up never holds: its last key is at 100%, reached on the last startup frame.</summary>
        public bool Flows { get; private set; }

        /// <summary>How many key poses the timeline has, counting the entry and any holds.</summary>
        public int Count => _frames.Count;

        /// <summary>The frame key <paramref name="i"/> is reached on (the entry is at -1).</summary>
        public int FrameOf(int i) => _frames[i];

        /// <summary>
        /// Builds the timeline for <paramref name="t"/> from its key poses (each list in order of percent): the wind-up keys
        /// over the startup, the strike keys over the active frames, the recovery keys and then <paramref name="stance"/>
        /// over the recovery.
        /// </summary>
        public static MoveTimeline Of(Technique t, IReadOnlyList<MoveKey> windUp, IReadOnlyList<MoveKey> strike,
            IReadOnlyList<MoveKey> recovery, Pose stance)
        {
            int s = t.StartupFrames, a = t.ActiveFrames, r = t.RecoveryFrames;
            var m = new MoveTimeline();
            m.Flows = windUp.Count > 0 && windUp[windUp.Count - 1].Percent >= 100;
            m.Add(-1, null, !m.Flows);

            // Wind-up: each key at its share of the startup, the last held to the end of it unless it flows.
            if (s > 0)
            {
                foreach (var k in windUp) m.Add(Math.Max(0, (s * k.Percent + 99) / 100 - 1), k.Pose, false, s - 1);
                if (windUp.Count > 0) m.HoldTo(s - 1, !m.Flows);
            }

            // Strike: snaps in on the first active frame; more keys carry it through the active frames.
            if (a > 0 && strike.Count > 0)
            {
                for (int i = 0; i < strike.Count; i++)
                {
                    var k = strike[i];
                    m.Add(i == 0 ? s : s + (a - 1) * k.Percent / 100, k.Pose, false, s + a - 1);
                }
                m.HoldTo(s + a - 1, strike[strike.Count - 1].Percent < 100);
            }

            // Recovery: through its keys, then into the stance on the last frame.
            if (r > 0)
            {
                int start = s + a;
                if (r > 1) foreach (var k in recovery) m.Add(start + (r * k.Percent + 99) / 100 - 1, k.Pose, false, start + r - 2);
                m.Add(start + r - 1, stance, true, start + r - 1);
            }

            // A key that repeats the one before it is a hold: the body stops on it, easing into it and out of it.
            for (int i = 1; i < m._poses.Count; i++)
            {
                var before = m._poses[i - 1];
                var key = m._poses[i];
                if (before != null && key != null && before.SameAngles(key)) m._rest[i - 1] = m._rest[i] = true;
            }
            return m;
        }

        /// <summary>
        /// Adds a key on <paramref name="frame"/>, after the last one and no later than <paramref name="latest"/>. Two keys
        /// that land on the same frame (a short phase) keep the later one.
        /// </summary>
        private void Add(int frame, Pose? pose, bool rest, int latest = int.MaxValue)
        {
            int last = _frames.Count - 1;
            if (last >= 0 && frame <= _frames[last]) frame = _frames[last] + 1;
            if (frame > latest)
            {
                if (last <= 0 || _frames[last] < latest) frame = latest;
                else
                {
                    // No frame left for both: the later key takes the last one's place.
                    _poses[last] = pose;
                    _rest[last] = rest;
                    return;
                }
            }
            _frames.Add(frame);
            _poses.Add(pose);
            _rest.Add(rest);
        }

        /// <summary>The last key held until <paramref name="frame"/> (a stop, if <paramref name="rest"/>).</summary>
        private void HoldTo(int frame, bool rest)
        {
            int last = _frames.Count - 1;
            _rest[last] = rest;
            if (_frames[last] < frame)
            {
                _frames.Add(frame);
                _poses.Add(_poses[last]);
                _rest.Add(rest);
            }
        }

        /// <summary>The pose on frame <paramref name="frame"/> of the move, which started from <paramref name="entry"/>.</summary>
        public Pose At(int frame, Pose entry)
        {
            if (frame <= _frames[0]) return entry;
            int i = 0;
            while (i + 1 < _frames.Count && _frames[i + 1] <= frame) i++;
            var from = _poses[i] ?? entry;
            if (i + 1 >= _frames.Count) return from;
            int x = (frame - _frames[i]) * 1000 / (_frames[i + 1] - _frames[i]);
            return Pose.Blend(from, _poses[i + 1] ?? entry, Shape(x, _rest[i], _rest[i + 1]));
        }

        /// <summary>Slow off a stop, slow into a stop, steady between moving keys (0-1000 in, 0-1000 out).</summary>
        public static int Shape(int x, bool fromRest, bool toRest)
        {
            if (fromRest && toRest) return Pose.Ease(x);
            if (fromRest) return x * x / 1000;
            if (toRest) return 1000 - (1000 - x) * (1000 - x) / 1000;
            return x;
        }
    }
}
