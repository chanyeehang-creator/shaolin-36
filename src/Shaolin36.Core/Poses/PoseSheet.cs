using System;
using System.Collections.Generic;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// The pose sheet: the mannequin's key poses, and which pose the fighter shows on each frame.
    ///
    /// Each move needs two poses, its wind-up and its strike, looked up by name: "&lt;technique id&gt;.windup" and
    /// "&lt;technique id&gt;.strike" first, then the category's ("waigong.windup", "waigong.strike", ...). So every
    /// category has a default and any one move can have its own. The other states use the <see cref="BasePoses"/>.
    /// Pose only: none of this changes the rules (the hitboxes stay where the technique sheet puts them).
    /// </summary>
    public sealed class PoseSheet
    {
        /// <summary>Poses every sheet must have, for the states that aren't moves.</summary>
        public static readonly string[] BasePoses = { "stance", "hitstun", "stagger", "grabbed", "down", "step" };

        public const string WindUpSuffix = ".windup";
        public const string StrikeSuffix = ".strike";

        private readonly Dictionary<string, Pose> _poses;

        public PoseSheet(IEnumerable<Pose> poses)
        {
            _poses = new Dictionary<string, Pose>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in poses) _poses[p.Name] = p;
            Stance = Find("stance") ?? new Pose("stance");
        }

        public int Count => _poses.Count;

        public IEnumerable<string> Names => _poses.Keys;

        public Pose Stance { get; }

        public Pose? Find(string name) => _poses.TryGetValue(name, out var p) ? p : null;

        private Pose Base(string name) => Find(name) ?? Stance;

        /// <summary>The category's name in the sheet ("waigong", "fangshen", ...).</summary>
        public static string CategoryName(Category c) => c.ToString().ToLowerInvariant();

        public Pose WindUpOf(Technique t) => Find(t.Id + WindUpSuffix) ?? Find(CategoryName(t.Category) + WindUpSuffix) ?? Stance;

        public Pose StrikeOf(Technique t) => Find(t.Id + StrikeSuffix) ?? Find(CategoryName(t.Category) + StrikeSuffix) ?? Stance;

        /// <summary>
        /// Problems with the sheet against the techniques: a base pose missing, a move with no wind-up or strike pose,
        /// or a move pose named after a technique or category that doesn't exist (a typo would be silently ignored).
        /// </summary>
        public List<string> Check(IEnumerable<Technique> techniques)
        {
            var errors = new List<string>();
            foreach (var name in BasePoses)
                if (Find(name) == null) errors.Add($"missing pose '{name}'");

            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Category c in Enum.GetValues(typeof(Category))) owners.Add(CategoryName(c));
            foreach (var t in techniques)
            {
                owners.Add(t.Id);
                if (Find(t.Id + WindUpSuffix) == null && Find(CategoryName(t.Category) + WindUpSuffix) == null)
                    errors.Add($"'{t.Id}' has no wind-up pose: add '{t.Id}{WindUpSuffix}' or '{CategoryName(t.Category)}{WindUpSuffix}'");
                if (Find(t.Id + StrikeSuffix) == null && Find(CategoryName(t.Category) + StrikeSuffix) == null)
                    errors.Add($"'{t.Id}' has no strike pose: add '{t.Id}{StrikeSuffix}' or '{CategoryName(t.Category)}{StrikeSuffix}'");
            }

            foreach (var name in _poses.Keys)
            {
                int dot = name.LastIndexOf('.');
                if (dot < 0)
                {
                    if (Array.IndexOf(BasePoses, name.ToLowerInvariant()) < 0)
                        errors.Add($"pose '{name}' is not used: name it after a state ({string.Join(", ", BasePoses)}) or '<technique or category>{WindUpSuffix}' / '{StrikeSuffix}'");
                    continue;
                }
                var owner = name.Substring(0, dot);
                var suffix = name.Substring(dot);
                if (!suffix.Equals(WindUpSuffix, StringComparison.OrdinalIgnoreCase) && !suffix.Equals(StrikeSuffix, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"pose '{name}' must end in {WindUpSuffix} or {StrikeSuffix}");
                else if (!owners.Contains(owner))
                    errors.Add($"pose '{name}': no technique or category called '{owner}'");
            }
            return errors;
        }

        /// <summary>
        /// The pose a fighter shows this frame. <paramref name="entry"/> is what it showed when its current move started
        /// (so a string's next hit winds up from the last hit, not from the stance); leave it null to start from the stance.
        /// </summary>
        public Pose PoseAt(Fighter f, Pose? entry = null)
        {
            if (f.IsDown) return Base("down");
            switch (f.State)
            {
                case FighterState.Startup:
                case FighterState.Active:
                case FighterState.Recovery:
                    return MovePose(f.Technique!, f.MoveFrame, entry);
                case FighterState.Hitstun:
                case FighterState.Recoil:
                    return Base("hitstun");
                case FighterState.Stagger: return Base("stagger");
                case FighterState.Grabbed: return Base("grabbed");
                case FighterState.KnockedDown:
                case FighterState.Rolling:
                    return Base("down");
                case FighterState.Rising:
                    return Pose.Blend(Base("down"), Stance, Pose.Ease((f.GetUpFrame + 1) * 1000 / Math.Max(1, f.GetUpTotalFrames)));
                case FighterState.QuickStep: return Base("step");
                default: return Stance;
            }
        }

        /// <summary>The pose on frame <paramref name="frame"/> of a move (see <see cref="MoveTimeline"/>).</summary>
        public Pose MovePose(Technique t, int frame, Pose? entry = null)
        {
            var step = MoveTimeline.At(t, frame);
            return Pose.Blend(Key(step.From, t, entry), Key(step.To, t, entry), step.Permille);
        }

        private Pose Key(PoseKey key, Technique t, Pose? entry)
        {
            switch (key)
            {
                case PoseKey.Entry: return entry ?? Stance;
                case PoseKey.WindUp: return WindUpOf(t);
                case PoseKey.Strike: return StrikeOf(t);
                default: return Stance;
            }
        }
    }
}
