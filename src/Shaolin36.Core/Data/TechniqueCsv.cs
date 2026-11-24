using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shaolin36.Core.Data
{
    /// <summary>
    /// Reads the technique sheet (CSV exported from the xlsx) and checks every row.
    /// Bad rows are reported, never silently loaded.
    /// </summary>
    public static class TechniqueCsv
    {
        public static readonly string[] Columns =
        {
            "id", "name", "category", "weight", "weapon",
            "startup", "active", "recovery", "clash_window", "perfect_clash",
            "stamina", "qi", "damage", "clashable",
            "hitstun", "reach", "next", "chain_window", "perfect_parry",
        };

        /// <summary>Columns a sheet may leave out; an empty cell means the default (no empowered version).</summary>
        public static readonly string[] OptionalColumns = { "empower_qi", "empower_damage_pct", "empower_armor", "knockback", "hit_angle" };

        /// <summary>Most a hit can push the defender back, in centimetres.</summary>
        public const int MaxKnockback = 500;

        private static readonly Dictionary<string, Category> CategoryNames =
            new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
            {
                ["waigong"] = Category.Waigong, ["外功"] = Category.Waigong,
                ["neigong"] = Category.Neigong, ["内功"] = Category.Neigong,
                ["qinggong"] = Category.Qinggong, ["轻功"] = Category.Qinggong,
                ["dianxue"] = Category.Dianxue, ["点穴"] = Category.Dianxue,
                ["huajin"] = Category.Huajin, ["化劲"] = Category.Huajin,
                ["qinna"] = Category.Qinna, ["擒拿"] = Category.Qinna,
                ["qigong"] = Category.Qigong, ["气功"] = Category.Qigong,
            };

        private static readonly Dictionary<string, Weight> WeightNames =
            new Dictionary<string, Weight>(StringComparer.OrdinalIgnoreCase)
            {
                ["light"] = Weight.Light, ["轻"] = Weight.Light,
                ["medium"] = Weight.Medium, ["中"] = Weight.Medium,
                ["heavy"] = Weight.Heavy, ["重"] = Weight.Heavy,
            };

        public sealed class Result
        {
            public Result(List<Technique> techniques, List<string> errors)
            {
                Techniques = techniques;
                Errors = errors;
            }

            public List<Technique> Techniques { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var techniques = new List<Technique>();
            var errors = new List<string>();
            var lines = CsvText.SplitLines(text);

            int headerIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!CsvText.IsBlankOrComment(lines[i])) { headerIndex = i; break; }
            }

            if (headerIndex < 0)
            {
                errors.Add("line 1: file is empty, expected a header row");
                return new Result(techniques, errors);
            }

            var header = CsvText.SplitRow(lines[headerIndex]);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < header.Count; c++) col[header[c].Trim()] = c;

            foreach (var name in Columns)
            {
                if (!col.ContainsKey(name)) errors.Add($"line {headerIndex + 1}: missing column '{name}'");
            }

            if (errors.Count > 0) return new Result(techniques, errors);

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var chains = new List<(int line, string id, string next)>();

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (CsvText.IsBlankOrComment(lines[i])) continue;

                int lineNo = i + 1;
                var cells = CsvText.SplitRow(lines[i]);
                var rowErrors = new List<string>();

                string Cell(string name)
                {
                    if (!col.ContainsKey(name)) return string.Empty;
                    int idx = col[name];
                    return idx < cells.Count ? cells[idx].Trim() : string.Empty;
                }

                int Int(string name)
                {
                    var raw = Cell(name);
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0) return v;
                    rowErrors.Add($"'{name}' must be a whole number 0 or more, got '{raw}'");
                    return 0;
                }

                bool Bool(string name)
                {
                    var raw = Cell(name).ToLowerInvariant();
                    switch (raw)
                    {
                        case "true": case "yes": case "y": case "1": return true;
                        case "false": case "no": case "n": case "0": return false;
                    }
                    rowErrors.Add($"'{name}' must be true or false, got '{Cell(name)}'");
                    return false;
                }

                var id = Cell("id");
                var displayName = Cell("name");
                if (id.Length == 0) rowErrors.Add("'id' is empty");
                else if (!seenIds.Add(id)) rowErrors.Add($"duplicate id '{id}'");
                if (displayName.Length == 0) rowErrors.Add("'name' is empty");

                if (!CategoryNames.TryGetValue(Cell("category"), out var category))
                    rowErrors.Add($"unknown category '{Cell("category")}'");
                if (!WeightNames.TryGetValue(Cell("weight"), out var weight))
                    rowErrors.Add($"unknown weight '{Cell("weight")}' (light, medium or heavy)");

                bool weapon = Bool("weapon");
                int startup = Int("startup");
                int active = Int("active");
                int recovery = Int("recovery");
                int window = Int("clash_window");
                int perfect = Int("perfect_clash");
                int stamina = Int("stamina");
                int qi = Int("qi");
                int damage = Int("damage");
                bool clashable = Bool("clashable");
                int hitstun = Int("hitstun");
                int reach = Int("reach");
                var next = Cell("next");
                int chain = Int("chain_window");
                int perfectParry = Int("perfect_parry");
                int empowerQi = Cell("empower_qi").Length == 0 ? 0 : Int("empower_qi");
                int empowerPct = Cell("empower_damage_pct").Length == 0 ? 100 : Int("empower_damage_pct");
                bool empowerArmor = Cell("empower_armor").Length != 0 && Bool("empower_armor");
                int knockback = Cell("knockback").Length == 0 ? 0 : Int("knockback");
                int hitAngle = Cell("hit_angle").Length == 0 ? Technique.FullCircle : Int("hit_angle");

                if (startup < 1) rowErrors.Add("'startup' must be at least 1 frame");
                if (active < 1) rowErrors.Add("'active' must be at least 1 frame");
                if (window > startup) rowErrors.Add($"'clash_window' ({window}) cannot be longer than 'startup' ({startup})");
                if (perfect > window) rowErrors.Add($"'perfect_clash' ({perfect}) cannot be longer than 'clash_window' ({window})");
                if (!clashable && window > 0) rowErrors.Add("an unclashable technique must have 'clash_window' 0");
                if (chain > recovery) rowErrors.Add($"'chain_window' ({chain}) cannot be longer than 'recovery' ({recovery})");
                if (next.Length > 0 && chain == 0) rowErrors.Add($"'next' is '{next}' but 'chain_window' is 0, so the string can never continue");
                if (next.Length == 0 && chain > 0) rowErrors.Add("'chain_window' is set but 'next' is empty");
                if (next.Length > 0 && next == id) rowErrors.Add("'next' cannot be the technique itself");
                if (perfectParry > 0 && category != Category.Huajin) rowErrors.Add("only 化劲 (huajin) techniques can have 'perfect_parry'");
                if (perfectParry > active) rowErrors.Add($"'perfect_parry' ({perfectParry}) cannot be longer than 'active' ({active}), which is the parry window");

                if (empowerQi == 0 && (empowerPct != 100 || empowerArmor))
                    rowErrors.Add("'empower_damage_pct' or 'empower_armor' is set but 'empower_qi' is 0, so the empowered version can never happen");
                if (knockback > MaxKnockback) rowErrors.Add($"'knockback' ({knockback}) must be {MaxKnockback} cm or less");
                if (knockback > 0 && reach == 0) rowErrors.Add("'knockback' is set but 'reach' is 0, so the move never hits");
                if (hitAngle < 1 || hitAngle > Technique.FullCircle) rowErrors.Add($"'hit_angle' ({hitAngle}) must be between 1 and {Technique.FullCircle} degrees ({Technique.FullCircle} = all round)");
                if (empowerQi > 0 && empowerPct == 0) rowErrors.Add("'empower_damage_pct' must be above 0");

                if (rowErrors.Count > 0)
                {
                    foreach (var e in rowErrors) errors.Add($"line {lineNo} ({(id.Length > 0 ? id : "?")}): {e}");
                    continue;
                }

                if (next.Length > 0) chains.Add((lineNo, id, next));
                techniques.Add(new Technique(id, displayName, category, weight, weapon,
                    startup, active, recovery, window, perfect, stamina, qi, damage, clashable,
                    hitstun, reach, next, chain, perfectParry, empowerQi, empowerPct, empowerArmor, knockback, hitAngle));
            }

            foreach (var (line, id, next) in chains)
            {
                if (!seenIds.Contains(next)) errors.Add($"line {line} ({id}): 'next' is '{next}', but no technique has that id");
            }

            return new Result(techniques, errors);
        }
    }
}
