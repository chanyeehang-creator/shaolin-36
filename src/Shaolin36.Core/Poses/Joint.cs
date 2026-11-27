namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// The joints of the fighter mannequin. Every pose sets an angle on some of them (the rest stay at 0).
    /// Named like a humanoid rig's bones, so a real rigged model can take the same poses later.
    /// </summary>
    public enum Joint
    {
        /// <summary>Turn (yaw) of the whole body, legs and all; lean (pitch) and tilt (roll) bend the body at the waist, leaving the legs alone.</summary>
        Hips,
        /// <summary>The upper body, on top of the hips.</summary>
        Chest,
        Head,
        ArmR,
        ElbowR,
        ArmL,
        ElbowL,
        LegR,
        KneeR,
        LegL,
        KneeL,
        /// <summary>
        /// The whole figure turned over around the hips, for rolls and flips: pitch turns it forward (head first: 360 is a full
        /// forward roll or front flip, minus a backflip), roll turns it over to its right (a cartwheel), yaw spins it.
        /// Angles go to ±360, so key poses can carry it all the way round. Turned over, whatever part of the body is lowest
        /// rests on the floor instead of the feet.
        /// </summary>
        Body,
    }

    public static class Joints
    {
        public const int Count = 12;

        /// <summary>The joint's name in the pose sheet.</summary>
        public static string Name(Joint j)
        {
            switch (j)
            {
                case Joint.Hips: return "hips";
                case Joint.Chest: return "chest";
                case Joint.Head: return "head";
                case Joint.ArmR: return "arm_r";
                case Joint.ElbowR: return "elbow_r";
                case Joint.ArmL: return "arm_l";
                case Joint.ElbowL: return "elbow_l";
                case Joint.LegR: return "leg_r";
                case Joint.KneeR: return "knee_r";
                case Joint.LegL: return "leg_l";
                case Joint.KneeL: return "knee_l";
                default: return "body";
            }
        }

        public static bool TryParse(string name, out Joint joint)
        {
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(Name((Joint)i), name, System.StringComparison.OrdinalIgnoreCase))
                {
                    joint = (Joint)i;
                    return true;
                }
            }
            joint = Joint.Hips;
            return false;
        }
    }
}
