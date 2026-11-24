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
            bool clashable)
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

        public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

        /// <summary>First frame of the clash window (inclusive).</summary>
        public int ClashWindowStart => StartupFrames - ClashWindowFrames;

        public bool IsInClashWindow(int frame) =>
            Clashable && ClashWindowFrames > 0 && frame >= ClashWindowStart && frame < StartupFrames;

        public bool IsInPerfectClash(int frame) =>
            IsInClashWindow(frame) && frame < ClashWindowStart + PerfectClashFrames;

        public bool IsActive(int frame) => frame >= StartupFrames && frame < StartupFrames + ActiveFrames;

        public override string ToString() => $"{Id} ({Name})";
    }
}
