using System;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// One key pose of the mannequin: three angles in whole degrees for each <see cref="Joint"/>.
    /// <list type="bullet">
    /// <item>pitch: swing forward (arms, legs) or bend forward (hips, chest, head). For elbows and knees it is how far they bend.</item>
    /// <item>yaw: turn. For the hips, chest and head, plus turns the right shoulder forward (the body faces more to its left).</item>
    /// <item>roll: for arms and legs, swing out to the side, away from the body; for the hips, chest and head, tilt to the right.</item>
    /// </list>
    /// Left and right limbs use the same signs (a left arm with roll 30 swings out to the left), so poses mirror by swapping _l and _r.
    /// </summary>
    public sealed class Pose
    {
        private readonly int[] _angles;

        public Pose(string name) : this(name, new int[Joints.Count * 3]) { }

        private Pose(string name, int[] angles)
        {
            Name = name;
            _angles = angles;
        }

        public string Name { get; }

        public int Pitch(Joint j) => _angles[(int)j * 3];
        public int Yaw(Joint j) => _angles[(int)j * 3 + 1];
        public int Roll(Joint j) => _angles[(int)j * 3 + 2];

        internal void Set(Joint j, int pitch, int yaw, int roll)
        {
            _angles[(int)j * 3] = pitch;
            _angles[(int)j * 3 + 1] = yaw;
            _angles[(int)j * 3 + 2] = roll;
        }

        /// <summary>Most a pose angle can be, either way, in degrees.</summary>
        public const int MaxAngle = 180;

        /// <summary>Most a <see cref="Joint.Body"/> angle can be, either way: a full turn, so a roll or flip can go all the way round.</summary>
        public const int MaxBodyAngle = 360;

        /// <summary>Most an angle of <paramref name="j"/> can be, either way.</summary>
        public static int MaxAngleOf(Joint j) => j == Joint.Body ? MaxBodyAngle : MaxAngle;

        /// <summary>
        /// True if the whole figure is turned over (<see cref="Joint.Body"/> not at a whole number of turns): a roll or a flip.
        /// Then the body rests on its lowest part, not on its feet.
        /// </summary>
        public bool IsTurnedOver =>
            Pitch(Joint.Body) % 360 != 0 || Yaw(Joint.Body) % 360 != 0 || Roll(Joint.Body) % 360 != 0;

        /// <summary>
        /// The pose <paramref name="permille"/>/1000 of the way from <paramref name="from"/> to <paramref name="to"/>
        /// (0 = from, 1000 = to), each angle rounded to a whole degree. Pure integer maths, the same on every machine.
        /// The whole-body angles (<see cref="Joint.Body"/>) turn the shorter way round, whole turns counting as nothing: from 360
        /// (a roll just finished) to 0 doesn't spin back, and from 330 to 0 goes on forward 30. So a roll needs a key pose at
        /// least every 180 degrees (0, 120, 240, 360).
        /// </summary>
        public static Pose Blend(Pose from, Pose to, int permille)
        {
            if (permille <= 0) return from;
            if (permille >= 1000) return to;
            var angles = new int[Joints.Count * 3];
            int body = (int)Joint.Body * 3;
            for (int i = 0; i < angles.Length; i++)
            {
                int a = from._angles[i], b = to._angles[i];
                if (i >= body)
                {
                    while (b - a > 180) a += 360;
                    while (a - b > 180) a -= 360;
                }
                int d = (b - a) * permille;
                angles[i] = a + (d >= 0 ? (d + 500) / 1000 : -((-d + 500) / 1000));
            }
            return new Pose(to.Name, angles);
        }

        /// <summary>
        /// Ease in and out (smoothstep) on a 0-1000 scale: slow off the mark, fast in the middle, settling at the end.
        /// </summary>
        public static int Ease(int permille)
        {
            if (permille <= 0) return 0;
            if (permille >= 1000) return 1000;
            long x = permille;
            return (int)(x * x * (3000 - 2 * x) / 1_000_000);
        }

        public bool SameAngles(Pose other)
        {
            for (int i = 0; i < _angles.Length; i++)
                if (_angles[i] != other._angles[i]) return false;
            return true;
        }

        public override string ToString() => Name;

        internal static bool InRange(Joint j, int angle) => Math.Abs(angle) <= MaxAngleOf(j);
    }
}
