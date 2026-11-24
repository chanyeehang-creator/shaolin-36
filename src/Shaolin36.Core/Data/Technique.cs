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
            int perfectParryFrames = 0)
        {
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

        public override string ToString() => $"{Id} ({Name})";
    }
}
