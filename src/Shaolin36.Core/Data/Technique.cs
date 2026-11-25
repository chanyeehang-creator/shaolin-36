namespace Shaolin36.Core.Data
{
    /// <summary>
    /// One row of the technique sheet. Frames are sim ticks (see <see cref="Sim"/>).
    ///
    /// Timeline of a technique, frame index starting at 0:
    /// <code>
    ///   [ startup ........ ][ active ][ recovery ...... ]
    ///          [ clash window ]   ← the last ClashWindowFrames frames of startup
    ///          [perfect]          ← the first PerfectClashFrames of that window
    /// </code>
    /// For a 化劲 parry the active frames are the parry window, and its first
    /// PerfectParryFrames are the perfect parry.
    ///
    /// Empowered version (optional, <see cref="EmpowerQi"/> above 0): if the fighter has at least
    /// EmpowerQi Qi when the technique starts, it spends that Qi and the move is empowered: its hit
    /// deals <see cref="EmpowerDamagePct"/> of its damage and, with <see cref="EmpowerArmor"/>, it has
    /// hyper armor through its startup and active frames.
    /// </summary>
    public sealed class Technique
    {
        public Technique(
            string id,
            string name,
            Category category,
            Weight weight,
            bool isWeapon,
            int startupFrames,
            int activeFrames,
            int recoveryFrames,
            int clashWindowFrames,
            int perfectClashFrames,
            int staminaCost,
            int qiCost,
            int damage,
            bool clashable,
            int hitstunFrames = 0,
            int reachCm = 0,
            string next = "",
            int chainWindowFrames = 0,
            int perfectParryFrames = 0,
            int empowerQi = 0,
            int empowerDamagePct = 100,
            bool empowerArmor = false,
            int knockbackCm = 0,
            int hitAngle = FullCircle,
            int advanceCm = 0,
            int dashCm = 0,
            int invincibleFrames = 0)
        {
            DashCm = dashCm;
            InvincibleFrames = invincibleFrames;
            KnockbackCm = knockbackCm;
            AdvanceCm = advanceCm;
            HitAngle = hitAngle;
            // Worked out once from whole degrees and rounded to 1/1,000,000, so the arc is the same on every machine.
            _cosHalfAngle = System.Math.Round(System.Math.Cos(hitAngle * System.Math.PI / 360.0), 6);
            EmpowerQi = empowerQi;
            EmpowerDamagePct = empowerDamagePct;
            EmpowerArmor = empowerArmor;
            Id = id;
            Name = name;
            Category = category;
            Weight = weight;
            IsWeapon = isWeapon;
            StartupFrames = startupFrames;
            ActiveFrames = activeFrames;
            RecoveryFrames = recoveryFrames;
            ClashWindowFrames = clashWindowFrames;
            PerfectClashFrames = perfectClashFrames;
            StaminaCost = staminaCost;
            QiCost = qiCost;
            Damage = damage;
            Clashable = clashable;
            HitstunFrames = hitstunFrames;
            ReachCm = reachCm;
            Next = next;
            ChainWindowFrames = chainWindowFrames;
            PerfectParryFrames = perfectParryFrames;
        }

        public string Id { get; }
        public string Name { get; }
        public Category Category { get; }
        public Weight Weight { get; }
        public bool IsWeapon { get; }
        public int StartupFrames { get; }
        public int ActiveFrames { get; }
        public int RecoveryFrames { get; }
        public int ClashWindowFrames { get; }
        public int PerfectClashFrames { get; }
        public int StaminaCost { get; }
        public int QiCost { get; }
        public int Damage { get; }

        /// <summary>False for 擒拿 grabs, unblockable moves and 点穴 Qi strikes.</summary>
        public bool Clashable { get; }

        /// <summary>How long the defender is frozen after this hit lands, counted from the hit frame.</summary>
        public int HitstunFrames { get; }

        /// <summary>How far the hit reaches, in whole centimetres.</summary>
        public int ReachCm { get; }

        /// <summary>Id of the next hit in the string, or empty if this hit ends it.</summary>
        public string Next { get; }

        /// <summary>The first N recovery frames in which pressing again starts <see cref="Next"/>.</summary>
        public int ChainWindowFrames { get; }

        /// <summary>化劲 only: the first N active frames that count as a perfect parry.</summary>
        public int PerfectParryFrames { get; }

        /// <summary>Qi needed (and spent) for the empowered version when the move starts; 0 means it has none.</summary>
        public int EmpowerQi { get; }

        /// <summary>Damage of the empowered version as a percentage of <see cref="Damage"/> (200 = double).</summary>
        public int EmpowerDamagePct { get; }

        /// <summary>True if the empowered version has hyper armor: hits still deal damage but don't interrupt it.</summary>
        public bool EmpowerArmor { get; }

        /// <summary>How far a landed hit pushes the defender back, in whole centimetres (slid over knockback_frames).</summary>
        public int KnockbackCm { get; }

        /// <summary>
        /// How far the attacker steps forward during the move, in whole centimetres: along the aim, slid over the
        /// startup frames so the step is done as the hit comes out. The step stops at min_distance like walking.
        /// </summary>
        public int AdvanceCm { get; }

        /// <summary>
        /// 轻功 dash: how far the move carries the fighter, in whole centimetres, slid over its startup and active
        /// frames. The way it goes is the stick held on the tick it starts (relative to the lock-on), or straight at
        /// the other fighter when no direction is held. 0 = not a dash.
        /// </summary>
        public int DashCm { get; }

        /// <summary>Invincible frames: for the first N frames of the move, strikes pass through the fighter. 0 = none.</summary>
        public int InvincibleFrames { get; }

        public bool IsDash => DashCm > 0;

        /// <summary>Frames a dash slides over: its startup and active frames.</summary>
        public int DashFrames => StartupFrames + ActiveFrames;

        public bool IsInvincible(int frame) => frame >= 0 && frame < InvincibleFrames;

        /// <summary>A hit angle this wide covers every direction: no angle check.</summary>
        public const int FullCircle = 360;

        private readonly double _cosHalfAngle;

        /// <summary>
        /// How wide the hit is, in degrees, centred on where the move is aimed (90 = 45 degrees to either side).
        /// A target outside it is missed even inside the reach, so a step to the side can slip a strike.
        /// <see cref="FullCircle"/> (360) hits all round.
        /// </summary>
        public int HitAngle { get; }

        /// <summary>
        /// True if a target at <paramref name="offset"/> from the attacker is inside the hit angle of a move
        /// aimed along <paramref name="facing"/>. Reach is checked separately. A target on the very same spot is covered.
        /// </summary>
        public bool Covers(FloorPoint facing, FloorPoint offset)
        {
            if (HitAngle >= FullCircle || offset.IsZero || facing.IsZero) return true;
            double dot = FloorPoint.Dot(facing, offset);
            return dot >= _cosHalfAngle * facing.Length * offset.Length;
        }

        public bool CanEmpower => EmpowerQi > 0;

        public bool HasNext => Next.Length > 0;

        public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

        /// <summary>First frame of the clash window (inclusive).</summary>
        public int ClashWindowStart => StartupFrames - ClashWindowFrames;

        public bool IsInClashWindow(int frame) =>
            Clashable && ClashWindowFrames > 0 && frame >= ClashWindowStart && frame < StartupFrames;

        public bool IsInPerfectClash(int frame) =>
            IsInClashWindow(frame) && frame < ClashWindowStart + PerfectClashFrames;

        public bool IsActive(int frame) => frame >= StartupFrames && frame < StartupFrames + ActiveFrames;

        /// <summary>A parry deflects strikes during its active frames.</summary>
        public bool IsParrying(int frame) => Category == Category.Huajin && PerfectParryFrames > 0 && IsActive(frame);

        public bool IsInPerfectParry(int frame) => IsParrying(frame) && frame < StartupFrames + PerfectParryFrames;

        /// <summary>
        /// How many of the chain window's frames give a true combo into <paramref name="next"/>:
        /// chaining on recovery frame c lands the next hit (ActiveFrames + c + next.StartupFrames)
        /// frames after this one, which must be before this hit's hitstun ends.
        /// 0 means the string never links, so the defender always gets a gap.
        /// </summary>
        public int LinkFramesInto(Technique next)
        {
            int frames = HitstunFrames - ActiveFrames - next.StartupFrames;
            if (frames < 0) frames = 0;
            return frames < ChainWindowFrames ? frames : ChainWindowFrames;
        }

        /// <summary>
        /// For a string starting with this technique: the farthest away the defender can stand when it starts
        /// for every hit to stay in reach, given each hit's step forward and each landed hit's knockback (which
        /// has finished sliding by the next hit when knockback_frames is shorter than the gaps). Assumes the
        /// defender stands still on the line of the aim. <paramref name="find"/> looks up the next hits by id.
        /// Hits whose next id is missing end the string.
        /// </summary>
        public int StringReachFrom(System.Func<string, Technique?> find)
        {
            int best = int.MaxValue, closed = 0, guard = 0;
            for (Technique? t = this; t != null && guard < 32; t = t.HasNext ? find(t.Next) : null, guard++)
            {
                closed += t.AdvanceCm;
                best = System.Math.Min(best, t.ReachCm + closed);
                closed -= t.KnockbackCm;
            }
            return best;
        }

        public override string ToString() => $"{Id} ({Name})";
    }
}
