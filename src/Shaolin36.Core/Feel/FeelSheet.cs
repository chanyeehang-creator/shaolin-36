using System;
using System.Collections.Generic;
using System.Globalization;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Feel
{
    /// <summary>The kinds of strike the feel kit tells apart. Each is one row of the feel sheet.</summary>
    public enum FeelEvent
    {
        /// <summary>A light or medium hit lands.</summary>
        Hit,
        /// <summary>A heavy hit lands.</summary>
        HeavyHit,
        /// <summary>A Qi-empowered hit lands.</summary>
        EmpoweredHit,
        /// <summary>A hit lands on hyper armor: damage, no flinch.</summary>
        ArmorHit,
        /// <summary>A 化劲 parry blocks the hit (not perfect).</summary>
        Block,
        /// <summary>A perfect parry.</summary>
        PerfectParry,
        /// <summary>Same-weight clash: both recoil.</summary>
        ClashRecoil,
        /// <summary>The heavier move wins its clash and its hit lands.</summary>
        ClashBreak,
        /// <summary>The lighter striker is knocked aside in a clash.</summary>
        ClashLost,
        /// <summary>A perfect clash.</summary>
        PerfectClash,
        /// <summary>The hit that takes the last HP.</summary>
        Ko,
        /// <summary>A strike passes through a 轻功 dash's invincible frames (a just dodge).</summary>
        JustDodge,
        /// <summary>A 擒拿 grab catches (the hold starts).</summary>
        Grab,
        /// <summary>A 擒拿 grab's throw lands (and 龙爪手's knockdown strike).</summary>
        Throw,
        /// <summary>Two pure energy moves meet and cancel out (狮吼功 into 狮吼功).</summary>
        EnergyCancel,
        /// <summary>A held fighter breaks out of a grab with a grab of their own, or slips a 龙爪手 that ran out of taps.</summary>
        GrabBreak,
    }

    /// <summary>The sounds the game can make for a strike (synthesised in game until there are real sound files).</summary>
    public enum FeelSound
    {
        None,
        Thud,
        Smack,
        Crack,
        Block,
        Ring,
        Clang,
        Gong,
        /// <summary>A light rush of air, for a just dodge.</summary>
        Whoosh,
    }

    /// <summary>One row of the feel sheet: what one kind of strike looks, sounds and feels like.</summary>
    public sealed class FeelRow
    {
        public FeelRow(FeelEvent @event, int hitStopFrames, int shakeCm, int pushCm, int sparkCm, FeelSound sound, int slowFrames = 0)
        {
            SlowFrames = slowFrames;
            Event = @event;
            HitStopFrames = hitStopFrames;
            ShakeCm = shakeCm;
            PushCm = pushCm;
            SparkCm = sparkCm;
            Sound = sound;
        }

        public FeelEvent Event { get; }

        /// <summary>Sim ticks the whole fight freezes right after the strike.</summary>
        public int HitStopFrames { get; }

        /// <summary>Camera shake, in centimetres.</summary>
        public int ShakeCm { get; }

        /// <summary>Camera push toward the strike, in centimetres.</summary>
        public int PushCm { get; }

        /// <summary>Size of the spark burst, in centimetres (0 = none).</summary>
        public int SparkCm { get; }

        public FeelSound Sound { get; }

        /// <summary>
        /// Sim ticks the game then plays at half speed (a slowdown). Only the screen slows: the rules still run tick by
        /// tick, so frame data and stamina are unchanged. The playtest slows only for your own just dodge.
        /// </summary>
        public int SlowFrames { get; }
    }

    /// <summary>The feel sheet (game/data/feel.csv): one row per <see cref="FeelEvent"/>.</summary>
    public sealed class FeelSheet
    {
        public const int MaxHitStop = 30;
        public const int MaxShake = 50;
        public const int MaxPush = 100;
        public const int MaxSpark = 200;
        public const int MaxSlow = 120;

        /// <summary>The sheet name of every event, in sheet order.</summary>
        public static readonly IReadOnlyDictionary<string, FeelEvent> EventNames = new Dictionary<string, FeelEvent>(StringComparer.Ordinal)
        {
            ["hit"] = FeelEvent.Hit,
            ["heavy_hit"] = FeelEvent.HeavyHit,
            ["empowered_hit"] = FeelEvent.EmpoweredHit,
            ["armor_hit"] = FeelEvent.ArmorHit,
            ["block"] = FeelEvent.Block,
            ["perfect_parry"] = FeelEvent.PerfectParry,
            ["clash_recoil"] = FeelEvent.ClashRecoil,
            ["clash_break"] = FeelEvent.ClashBreak,
            ["clash_lost"] = FeelEvent.ClashLost,
            ["perfect_clash"] = FeelEvent.PerfectClash,
            ["ko"] = FeelEvent.Ko,
            ["just_dodge"] = FeelEvent.JustDodge,
            ["grab"] = FeelEvent.Grab,
            ["throw"] = FeelEvent.Throw,
            ["energy_cancel"] = FeelEvent.EnergyCancel,
            ["grab_break"] = FeelEvent.GrabBreak,
        };

        public static readonly string[] Columns = { "event", "hitstop", "shake", "push", "spark", "sound" };

        private readonly Dictionary<FeelEvent, FeelRow> _rows;

        internal FeelSheet(Dictionary<FeelEvent, FeelRow> rows) => _rows = rows;

        public FeelRow this[FeelEvent e] => _rows[e];

        /// <summary>A sheet with every event at zero: no hit-stop, no effects. Handy for tests and for turning the kit off.</summary>
        public static FeelSheet Silent()
        {
            var rows = new Dictionary<FeelEvent, FeelRow>();
            foreach (FeelEvent e in Enum.GetValues(typeof(FeelEvent))) rows[e] = new FeelRow(e, 0, 0, 0, 0, FeelSound.None);
            return new FeelSheet(rows);
        }
    }

    /// <summary>Reads and checks the feel sheet. Bad rows are reported, never loaded.</summary>
    public static class FeelCsv
    {
        public sealed class Result
        {
            public Result(FeelSheet? sheet, List<string> errors)
            {
                Sheet = sheet;
                Errors = errors;
            }

            /// <summary>Null when there are errors.</summary>
            public FeelSheet? Sheet { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var errors = new List<string>();
            var rows = new Dictionary<FeelEvent, FeelRow>();
            var lineOf = new Dictionary<FeelEvent, int>();
            var lines = CsvText.SplitLines(text);

            int headerIndex = lines.FindIndex(l => !CsvText.IsBlankOrComment(l));
            if (headerIndex < 0)
            {
                errors.Add("line 1: file is empty, expected a header row 'event,hitstop,shake,push,spark,sound'");
                return new Result(null, errors);
            }

            var header = CsvText.SplitRow(lines[headerIndex]);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < header.Count; c++) col[header[c].Trim()] = c;
            foreach (var name in FeelSheet.Columns)
            {
                if (!col.ContainsKey(name)) errors.Add($"line {headerIndex + 1}: missing column '{name}'");
            }
            if (errors.Count > 0) return new Result(null, errors);

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (CsvText.IsBlankOrComment(lines[i])) continue;
                int lineNo = i + 1;
                var cells = CsvText.SplitRow(lines[i]);
                string Cell(string name) => !col.ContainsKey(name) ? string.Empty : col[name] < cells.Count ? cells[col[name]].Trim() : string.Empty;

                var eventName = Cell("event");
                if (!FeelSheet.EventNames.TryGetValue(eventName, out var e))
                {
                    errors.Add($"line {lineNo}: unknown event '{eventName}' (expected one of {string.Join(", ", FeelSheet.EventNames.Keys)})");
                    continue;
                }
                if (lineOf.TryGetValue(e, out var first))
                {
                    errors.Add($"line {lineNo}: duplicate event '{eventName}' (first on line {first})");
                    continue;
                }
                lineOf[e] = lineNo;

                bool ok = true;
                int Number(string name, int max)
                {
                    var raw = Cell(name);
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= max) return v;
                    errors.Add($"line {lineNo} ({eventName}, {name}): must be a whole number from 0 to {max}, got '{raw}'");
                    ok = false;
                    return 0;
                }

                int hitStop = Number("hitstop", FeelSheet.MaxHitStop);
                int shake = Number("shake", FeelSheet.MaxShake);
                int push = Number("push", FeelSheet.MaxPush);
                int spark = Number("spark", FeelSheet.MaxSpark);
                int slow = col.ContainsKey("slow") && Cell("slow").Length > 0 ? Number("slow", FeelSheet.MaxSlow) : 0;

                var soundRaw = Cell("sound");
                if (!Enum.TryParse<FeelSound>(soundRaw, true, out var sound) || !Enum.IsDefined(typeof(FeelSound), sound)
                    || int.TryParse(soundRaw, out _))
                {
                    errors.Add($"line {lineNo} ({eventName}, sound): unknown sound '{soundRaw}' (expected none, thud, smack, crack, block, ring, clang, gong or whoosh)");
                    ok = false;
                }

                if (ok) rows[e] = new FeelRow(e, hitStop, shake, push, spark, sound, slow);
            }

            foreach (var pair in FeelSheet.EventNames)
            {
                if (!lineOf.ContainsKey(pair.Value)) errors.Add($"line {lines.Count}: missing event '{pair.Key}'");
            }

            return new Result(errors.Count == 0 ? new FeelSheet(rows) : null, errors);
        }
    }
}
