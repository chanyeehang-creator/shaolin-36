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
        };

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
            var lines = SplitLines(text);

            int headerIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!IsBlankOrComment(lines[i])) { headerIndex = i; break; }
            }

            if (headerIndex < 0)
            {
                errors.Add("line 1: file is empty, expected a header row");
                return new Result(techniques, errors);
            }

            var header = SplitRow(lines[headerIndex]);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < header.Count; c++) col[header[c].Trim()] = c;

            foreach (var name in Columns)
            {
                if (!col.ContainsKey(name)) errors.Add($"line {headerIndex + 1}: missing column '{name}'");
            }

            if (errors.Count > 0) return new Result(techniques, errors);

            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (IsBlankOrComment(lines[i])) continue;

                int lineNo = i + 1;
                var cells = SplitRow(lines[i]);
                var rowErrors = new List<string>();

                string Cell(string name)
                {
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

                if (startup < 1) rowErrors.Add("'startup' must be at least 1 frame");
                if (active < 1) rowErrors.Add("'active' must be at least 1 frame");
                if (window > startup) rowErrors.Add($"'clash_window' ({window}) cannot be longer than 'startup' ({startup})");
                if (perfect > window) rowErrors.Add($"'perfect_clash' ({perfect}) cannot be longer than 'clash_window' ({window})");
                if (!clashable && window > 0) rowErrors.Add("an unclashable technique must have 'clash_window' 0");

                if (rowErrors.Count > 0)
                {
                    foreach (var e in rowErrors) errors.Add($"line {lineNo} ({(id.Length > 0 ? id : "?")}): {e}");
                    continue;
                }

                techniques.Add(new Technique(id, displayName, category, weight, weapon,
                    startup, active, recovery, window, perfect, stamina, qi, damage, clashable));
            }

            return new Result(techniques, errors);
        }

        private static bool IsBlankOrComment(string line)
        {
            var t = line.Trim();
            return t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal);
        }

        private static List<string> SplitLines(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1); // Excel's UTF-8 BOM
            return new List<string>(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        }

        /// <summary>Splits one CSV row. Supports "quoted, fields" and "" escapes.</summary>
        internal static List<string> SplitRow(string line)
        {
            var cells = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else cur.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { cells.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(ch);
            }
            cells.Add(cur.ToString());
            return cells;
        }
    }
}
