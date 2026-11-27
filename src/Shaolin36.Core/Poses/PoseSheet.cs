using System;
using System.Collections.Generic;
using System.Globalization;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// The pose sheet: the mannequin's key poses, and which pose the fighter shows on each frame.
    ///
    /// Each move needs a wind-up and a strike, looked up by name: "&lt;technique id&gt;.windup" and
    /// "&lt;technique id&gt;.strike" first, then the category's ("waigong.windup", "waigong.strike", ...). So every
    /// category has a default and any one move can have its own. A move can also have more key poses, each reached a
    /// share of the way through its part of the move: "&lt;id&gt;.windup@30" (30% through the startup), "&lt;id&gt;.strike@100"
    /// (the last active frame), "&lt;id&gt;.recovery@50" (halfway through the recovery). A plain ".windup" is "@50" and a plain
    /// ".strike" is "@0"; see <see cref="MoveTimeline"/> for how they're timed. The other states use the <see cref="BasePoses"/>.
    /// Pose only: none of this changes the rules (the hitboxes stay where the technique sheet puts them).
    /// </summary>
    public sealed class PoseSheet
    {
        /// <summary>Poses every sheet must have, for the states that aren't moves.</summary>
        public static readonly string[] BasePoses = { "stance", "hitstun", "stagger", "grabbed", "down", "step" };

        public const string WindUpSuffix = ".windup";
        public const string StrikeSuffix = ".strike";
        public const string RecoverySuffix = ".recovery";

        private static readonly IReadOnlyList<MoveKey> NoKeys = new MoveKey[0];

        private readonly Dictionary<string, Pose> _poses;

        // Move keys by owner (technique id or category name) and phase, each list in order of percent.
        private readonly Dictionary<string, List<MoveKey>[]> _keys = new Dictionary<string, List<MoveKey>[]>(StringComparer.OrdinalIgnoreCase);

        public PoseSheet(IEnumerable<Pose> poses)
        {
            _poses = new Dictionary<string, Pose>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in poses) _poses[p.Name] = p;
            Stance = Find("stance") ?? new Pose("stance");
            foreach (var p in _poses.Values)
            {
                if (TryParseKey(p.Name, out var owner, out var phase, out int percent, out _) != KeyName.Ok) continue;
                if (!_keys.TryGetValue(owner, out var phases))
                {
                    phases = new[] { new List<MoveKey>(), new List<MoveKey>(), new List<MoveKey>() };
                    _keys[owner] = phases;
                }
                phases[(int)phase].Add(new MoveKey(p, phase, percent));
            }
            foreach (var phases in _keys.Values)
                foreach (var list in phases)
                    list.Sort((x, y) => x.Percent != y.Percent ? x.Percent.CompareTo(y.Percent) : string.CompareOrdinal(x.Pose.Name, y.Pose.Name));
        }

        public int Count => _poses.Count;

        public IEnumerable<string> Names => _poses.Keys;

        public Pose Stance { get; }

        public Pose? Find(string name) => _poses.TryGetValue(name, out var p) ? p : null;

        private Pose Base(string name) => Find(name) ?? Stance;

        /// <summary>The category's name in the sheet ("waigong", "fangshen", ...).</summary>
        public static string CategoryName(Category c) => c.ToString().ToLowerInvariant();

        private enum KeyName
        {
            Ok,
            NotAMoveKey,
            BadPercent,
        }

        /// <summary>
        /// Reads a move key's name: "&lt;owner&gt;.windup", ".strike" or ".recovery", optionally "@percent". A plain wind-up is
        /// at 50% and a plain strike at 0%; a recovery key must say its percent.
        /// </summary>
        private static KeyName TryParseKey(string name, out string owner, out MovePhase phase, out int percent, out string why)
        {
            owner = "";
            phase = MovePhase.WindUp;
            percent = 0;
            why = "";
            int dot = name.LastIndexOf('.');
            if (dot <= 0) return KeyName.NotAMoveKey;
            owner = name.Substring(0, dot);
            var suffix = name.Substring(dot);
            string? at = null;
            int sign = suffix.IndexOf('@');
            if (sign >= 0)
            {
                at = suffix.Substring(sign + 1);
                suffix = suffix.Substring(0, sign);
            }

            if (suffix.Equals(WindUpSuffix, StringComparison.OrdinalIgnoreCase))
            {
                phase = MovePhase.WindUp;
                percent = 50;
            }
            else if (suffix.Equals(StrikeSuffix, StringComparison.OrdinalIgnoreCase))
            {
                phase = MovePhase.Strike;
                percent = 0;
            }
            else if (suffix.Equals(RecoverySuffix, StringComparison.OrdinalIgnoreCase))
            {
                phase = MovePhase.Recovery;
            }
            else return KeyName.NotAMoveKey;

            if (at == null)
            {
                if (phase != MovePhase.Recovery) return KeyName.Ok;
                why = $"a recovery key needs its percent, like '{RecoverySuffix}@50' (the stance always comes last)";
                return KeyName.BadPercent;
            }
            if (!int.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out percent))
            {
                why = $"'@{at}' must be a whole percent";
                return KeyName.BadPercent;
            }
            switch (phase)
            {
                case MovePhase.WindUp:
                    if (percent <= 100) return KeyName.Ok;
                    why = $"'@{at}': a wind-up key is @0 to @100";
                    return KeyName.BadPercent;
                case MovePhase.Strike:
                    if (percent >= 1 && percent <= 100) return KeyName.Ok;
                    why = $"'@{at}': a strike key is @1 to @100 (plain '{StrikeSuffix}' is the pose the hit snaps into, @0)";
                    return KeyName.BadPercent;
                default:
                    if (percent >= 1 && percent <= 99) return KeyName.Ok;
                    why = $"'@{at}': a recovery key is @1 to @99 (the stance is always @100)";
                    return KeyName.BadPercent;
            }
        }

        /// <summary>
        /// The key poses <paramref name="t"/> uses in <paramref name="phase"/>, in order: its own if it has any for that phase,
        /// otherwise its category's.
        /// </summary>
        public IReadOnlyList<MoveKey> KeysOf(Technique t, MovePhase phase)
        {
            if (_keys.TryGetValue(t.Id, out var own) && own[(int)phase].Count > 0) return own[(int)phase];
            if (_keys.TryGetValue(CategoryName(t.Category), out var cat) && cat[(int)phase].Count > 0) return cat[(int)phase];
            return NoKeys;
        }

        /// <summary>The wind-up the move ends its startup in (its last wind-up key), or the stance if it has none.</summary>
        public Pose WindUpOf(Technique t)
        {
            var keys = KeysOf(t, MovePhase.WindUp);
            return keys.Count > 0 ? keys[keys.Count - 1].Pose : Stance;
        }

        /// <summary>The pose the hit snaps into on its first active frame, or the stance if it has none.</summary>
        public Pose StrikeOf(Technique t)
        {
            var keys = KeysOf(t, MovePhase.Strike);
            return keys.Count > 0 ? keys[0].Pose : Stance;
        }

        /// <summary>The move's key poses timed to its frame data.</summary>
        public MoveTimeline TimelineOf(Technique t) =>
            MoveTimeline.Of(t, KeysOf(t, MovePhase.WindUp), KeysOf(t, MovePhase.Strike), KeysOf(t, MovePhase.Recovery), Stance);

        /// <summary>
        /// Problems with the sheet against the techniques: a base pose missing, a move with no wind-up or strike pose,
        /// a move pose named after a technique or category that doesn't exist (a typo would be silently ignored), a bad
        /// key name or percent, or two keys at the same point.
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
                if (KeysOf(t, MovePhase.WindUp).Count == 0)
                    errors.Add($"'{t.Id}' has no wind-up pose: add '{t.Id}{WindUpSuffix}' or '{CategoryName(t.Category)}{WindUpSuffix}'");
                if (KeysOf(t, MovePhase.Strike).Count == 0)
                    errors.Add($"'{t.Id}' has no strike pose: add '{t.Id}{StrikeSuffix}' or '{CategoryName(t.Category)}{StrikeSuffix}'");
            }

            foreach (var name in _poses.Keys)
            {
                var parsed = TryParseKey(name, out var owner, out _, out _, out var why);
                if (parsed == KeyName.NotAMoveKey)
                {
                    if (name.IndexOf('.') >= 0)
                        errors.Add($"pose '{name}' must end in {WindUpSuffix}, {StrikeSuffix} or {RecoverySuffix}@<percent> (wind-up and strike keys may add @<percent> too)");
                    else if (Array.IndexOf(BasePoses, name.ToLowerInvariant()) < 0)
                        errors.Add($"pose '{name}' is not used: name it after a state ({string.Join(", ", BasePoses)}) or '<technique or category>{WindUpSuffix}' / '{StrikeSuffix}'");
                }
                else if (parsed == KeyName.BadPercent) errors.Add($"pose '{name}': {why}");
                else if (!owners.Contains(owner)) errors.Add($"pose '{name}': no technique or category called '{owner}'");
            }

            foreach (var pair in _keys)
            {
                foreach (var list in pair.Value)
                    for (int i = 1; i < list.Count; i++)
                        if (list[i].Percent == list[i - 1].Percent)
                            errors.Add($"poses '{list[i - 1].Pose.Name}' and '{list[i].Pose.Name}' are both {list[i].Percent}% through the same part of the move");
                var strikes = pair.Value[(int)MovePhase.Strike];
                if (strikes.Count > 0 && strikes[0].Percent != 0)
                    errors.Add($"'{pair.Key}' has strike keys but no '{pair.Key}{StrikeSuffix}': the pose the hit snaps into on its first active frame");
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
        public Pose MovePose(Technique t, int frame, Pose? entry = null) => TimelineOf(t).At(frame, entry ?? Stance);
    }
}
