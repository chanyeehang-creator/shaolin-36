using System;
using System.Collections.Generic;
using System.Globalization;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Brains
{
    /// <summary>
    /// One row of the brain sheet (game/data/brains.csv): the knobs that make an opponent brain
    /// slow or sharp, timid or aggressive. Percentages are chances out of 100; frames are sim ticks.
    /// </summary>
    public sealed class BrainStyle
    {
        public const int MaxReaction = 60;
        public const int MaxThinkEvery = 120;

        public BrainStyle(string id, string name, int reaction, int thinkEvery, int aggression, int punish, int chain,
            int parry, int perfect, int step, int circle, int restBelow, int heavy, int guess = 0, int heal = 0, int grab = 0, int clash = 0, int breakOut = 0)
        {
            BreakOut = breakOut;
            Clash = clash;
            Grab = grab;
            Heal = heal;
            Id = id;
            Name = name;
            Reaction = reaction;
            ThinkEvery = thinkEvery;
            Aggression = aggression;
            Punish = punish;
            Chain = chain;
            Parry = parry;
            Perfect = perfect;
            Step = step;
            Circle = circle;
            RestBelow = restBelow;
            Heavy = heavy;
            Guess = guess;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Frames between something happening and the brain noticing it (a person takes 15 to 20).</summary>
        public int Reaction { get; }

        /// <summary>In range with nothing to react to, it makes a new choice (attack, circle or wait) this often, in frames.</summary>
        public int ThinkEvery { get; }

        /// <summary>Chance, at each choice in range, that it attacks.</summary>
        public int Aggression { get; }

        /// <summary>Chance that it attacks when it sees you open (recovering, staggered or recoiling) and in reach.</summary>
        public int Punish { get; }

        /// <summary>Chance, at each link of a string, that it presses for the next hit.</summary>
        public int Chain { get; }

        /// <summary>Chance that it answers a strike it sees coming with its 化劲 parry.</summary>
        public int Parry { get; }

        /// <summary>When it parries, the chance it aims for the perfect part of the window (otherwise the plain block part).</summary>
        public int Perfect { get; }

        /// <summary>Chance that it quick-steps to the side to slip a strike it sees coming (tried if it didn't parry), and the chance per choice of a quick step forward to close a long gap.</summary>
        public int Step { get; }

        /// <summary>Chance, at each choice in range where it doesn't attack, that it circles round you instead of waiting.</summary>
        public int Circle { get; }

        /// <summary>Below this much stamina it stops attacking and backs off to breathe (it still defends).</summary>
        public int RestBelow { get; }

        /// <summary>Chance that it picks a heavy technique (such as 铁砂掌) over a lighter one when it attacks.</summary>
        public int Heavy { get; }

        /// <summary>
        /// Chance, at each choice in your reach where it doesn't attack, that it throws its parry on a guess, the way a
        /// person predicts a strike too fast to react to (罗汉拳 hit 1 lands in 13 frames). A wrong guess costs it the
        /// parry's stamina and leaves it open for 27 frames: bait it and punish.
        /// </summary>
        public int Guess { get; }

        /// <summary>
        /// Chance, rolled every think_every frames while it is hurt by at least a heal's worth and well out of your reach,
        /// that it channels its 气功 heal (仙辽术). Hit it during the wind-up or channel to cancel the heal.
        /// </summary>
        public int Heal { get; }

        /// <summary>
        /// Chance, at each choice in reach of its 擒拿 grab where the other fighter can be grabbed, that it grabs (rolled
        /// before <see cref="Aggression"/>). A grab beats a parry, so it is its answer to a fighter who guards a lot.
        /// </summary>
        public int Grab { get; }

        /// <summary>
        /// Chance that it answers a clashable strike it sees coming with a clash (拼招): one of its own strikes timed to land in the
        /// strike's clash window (rolled before <see cref="Parry"/>). <see cref="Perfect"/> is the chance it aims for the perfect frames.
        /// It needs a move quick enough to land in the window after its reaction time, so in practice it clashes slow strikes like 铁砂掌.
        /// </summary>
        public int Clash { get; }

        /// <summary>
        /// Chance, each time it is grabbed (and again at each 龙爪手 strike window), that it breaks out with a grab of its own, pressed
        /// its reaction time after the window opens. Too slow and the window closes first. 0 = never (the novice has no grab at all).
        /// </summary>
        public int BreakOut { get; }

        public override string ToString() => $"{Id} ({Name})";
    }

    /// <summary>The brain sheet: one <see cref="BrainStyle"/> per row.</summary>
    public sealed class BrainSheet
    {
        public static readonly string[] Columns =
        {
            "id", "name", "reaction", "think_every", "aggression", "punish", "chain", "parry", "perfect", "step", "circle",
            "rest_below", "heavy", "guess",
        };

        /// <summary>Columns a sheet may leave out (an empty or missing cell is 0).</summary>
        public static readonly string[] OptionalColumns = { "heal", "grab", "clash", "break" };

        private readonly List<BrainStyle> _styles;

        internal BrainSheet(List<BrainStyle> styles) => _styles = styles;

        /// <summary>Every style, in sheet order.</summary>
        public IReadOnlyList<BrainStyle> Styles => _styles;

        /// <summary>The style with this id, or null.</summary>
        public BrainStyle? Find(string id) => _styles.Find(s => s.Id == id);
    }

    /// <summary>Reads and checks the brain sheet. Bad rows are reported, never loaded.</summary>
    public static class BrainCsv
    {
        public sealed class Result
        {
            public Result(BrainSheet? sheet, List<string> errors)
            {
                Sheet = sheet;
                Errors = errors;
            }

            /// <summary>Null when there are errors.</summary>
            public BrainSheet? Sheet { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var errors = new List<string>();
            var styles = new List<BrainStyle>();
            var lineOf = new Dictionary<string, int>(StringComparer.Ordinal);
            var lines = CsvText.SplitLines(text);

            int headerIndex = lines.FindIndex(l => !CsvText.IsBlankOrComment(l));
            if (headerIndex < 0)
            {
                errors.Add($"line 1: file is empty, expected a header row '{string.Join(",", BrainSheet.Columns)}'");
                return new Result(null, errors);
            }

            var header = CsvText.SplitRow(lines[headerIndex]);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < header.Count; c++) col[header[c].Trim()] = c;
            foreach (var name in BrainSheet.Columns)
            {
                if (!col.ContainsKey(name)) errors.Add($"line {headerIndex + 1}: missing column '{name}'");
            }
            if (errors.Count > 0) return new Result(null, errors);

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (CsvText.IsBlankOrComment(lines[i])) continue;
                int lineNo = i + 1;
                var cells = CsvText.SplitRow(lines[i]);
                string Cell(string name) => col.ContainsKey(name) && col[name] < cells.Count ? cells[col[name]].Trim() : string.Empty;

                var id = Cell("id");
                if (id.Length == 0)
                {
                    errors.Add($"line {lineNo}: id is empty");
                    continue;
                }
                if (lineOf.TryGetValue(id, out var first))
                {
                    errors.Add($"line {lineNo}: duplicate id '{id}' (first on line {first})");
                    continue;
                }
                lineOf[id] = lineNo;

                bool ok = true;
                int Number(string name, int min, int max, string unit)
                {
                    var raw = Cell(name);
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= min && v <= max) return v;
                    errors.Add($"line {lineNo} ({id}, {name}): must be a whole number from {min} to {max}{unit}, got '{raw}'");
                    ok = false;
                    return 0;
                }
                int Pct(string name) => Number(name, 0, 100, " (a chance out of 100)");

                int reaction = Number("reaction", 0, BrainStyle.MaxReaction, " frames");
                int thinkEvery = Number("think_every", 1, BrainStyle.MaxThinkEvery, " frames");
                int aggression = Pct("aggression");
                int punish = Pct("punish");
                int chain = Pct("chain");
                int parry = Pct("parry");
                int perfect = Pct("perfect");
                int step = Pct("step");
                int circle = Pct("circle");
                int restBelow = Number("rest_below", 0, 1000, " stamina");
                int heavy = Pct("heavy");
                int guess = Pct("guess");
                int heal = Cell("heal").Length == 0 ? 0 : Pct("heal");
                int grab = Cell("grab").Length == 0 ? 0 : Pct("grab");
                int clash = Cell("clash").Length == 0 ? 0 : Pct("clash");
                int breakOut = Cell("break").Length == 0 ? 0 : Pct("break");
                var name = Cell("name");

                if (ok)
                    styles.Add(new BrainStyle(id, name.Length > 0 ? name : id, reaction, thinkEvery, aggression, punish, chain,
                        parry, perfect, step, circle, restBelow, heavy, guess, heal, grab, clash, breakOut));
            }

            if (errors.Count == 0 && styles.Count == 0) errors.Add($"line {lines.Count}: no brain rows, expected at least one");

            return new Result(errors.Count == 0 ? new BrainSheet(styles) : null, errors);
        }
    }
}
