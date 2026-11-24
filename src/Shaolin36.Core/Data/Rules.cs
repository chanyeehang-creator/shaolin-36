using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shaolin36.Core.Data
{
    /// <summary>
    /// The rules sheet (game/data/rules.csv): base fighter stats and the numbers
    /// behind every clash and parry result. Frames are sim ticks, stamina and Qi
    /// are points, rates are points per second.
    /// </summary>
    public sealed class Rules
    {
        /// <summary>Every key the sheet must have, in sheet order.</summary>
        public static readonly string[] Keys =
        {
            "hp_max", "stamina_max", "stamina_regen", "stamina_regen_still", "stamina_regen_delay",
            "exhausted_until", "exhausted_damage_pct", "qi_max", "qi_start", "qi_per_hit",
            "clash_recoil_frames", "clash_recoil_stamina", "clash_lose_stagger", "clash_lose_stamina",
            "clash_perfect_stagger", "clash_perfect_recovery", "clash_perfect_qi",
            "parry_recovery", "parry_perfect_recovery", "parry_perfect_stagger", "parry_perfect_drain", "parry_perfect_qi",
            "input_buffer",
        };

        private readonly Dictionary<string, int> _values;

        internal Rules(Dictionary<string, int> values) => _values = values;

        private int V(string key) => _values[key];

        public int HpMax => V("hp_max");
        public int StaminaMax => V("stamina_max");
        /// <summary>Stamina regained per second while not acting.</summary>
        public int StaminaRegen => V("stamina_regen");
        /// <summary>Stamina regained per second while standing still.</summary>
        public int StaminaRegenStill => V("stamina_regen_still");
        /// <summary>Frames after the last action before stamina starts to regenerate.</summary>
        public int StaminaRegenDelay => V("stamina_regen_delay");
        /// <summary>An exhausted fighter stays exhausted until stamina refills to this.</summary>
        public int ExhaustedUntil => V("exhausted_until");
        /// <summary>Damage taken while exhausted, as a percentage of normal (125 = +25%).</summary>
        public int ExhaustedDamagePct => V("exhausted_damage_pct");
        public int QiMax => V("qi_max");
        public int QiStart => V("qi_start");
        public int QiPerHit => V("qi_per_hit");

        public int ClashRecoilFrames => V("clash_recoil_frames");
        public int ClashRecoilStamina => V("clash_recoil_stamina");
        public int ClashLoseStagger => V("clash_lose_stagger");
        public int ClashLoseStamina => V("clash_lose_stamina");
        public int ClashPerfectStagger => V("clash_perfect_stagger");
        public int ClashPerfectRecovery => V("clash_perfect_recovery");
        public int ClashPerfectQi => V("clash_perfect_qi");

        public int ParryRecovery => V("parry_recovery");
        public int ParryPerfectRecovery => V("parry_perfect_recovery");
        public int ParryPerfectStagger => V("parry_perfect_stagger");
        public int ParryPerfectDrain => V("parry_perfect_drain");
        public int ParryPerfectQi => V("parry_perfect_qi");

        /// <summary>
        /// A press that can't be used on the tick it's made (the fighter is busy) is kept and
        /// tried again on each of the next this-many ticks. 0 turns the buffer off.
        /// </summary>
        public int InputBuffer => V("input_buffer");
    }

    /// <summary>Reads and checks the rules sheet: columns key,value,note. Bad rows are reported, never loaded.</summary>
    public static class RulesCsv
    {
        public sealed class Result
        {
            public Result(Rules? rules, List<string> errors)
            {
                Rules = rules;
                Errors = errors;
            }

            /// <summary>Null when there are errors.</summary>
            public Rules? Rules { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var errors = new List<string>();
            var lines = CsvText.SplitLines(text);
            var known = new HashSet<string>(Rules.Keys, StringComparer.Ordinal);
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            var lineOf = new Dictionary<string, int>(StringComparer.Ordinal);

            int headerIndex = lines.FindIndex(l => !CsvText.IsBlankOrComment(l));
            if (headerIndex < 0)
            {
                errors.Add("line 1: file is empty, expected a header row 'key,value,note'");
                return new Result(null, errors);
            }

            var header = CsvText.SplitRow(lines[headerIndex]);
            if (header.Count < 2 || header[0].Trim() != "key" || header[1].Trim() != "value")
            {
                errors.Add($"line {headerIndex + 1}: header must start with 'key,value'");
                return new Result(null, errors);
            }

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (CsvText.IsBlankOrComment(lines[i])) continue;
                int lineNo = i + 1;
                var cells = CsvText.SplitRow(lines[i]);
                var key = cells[0].Trim();
                var raw = cells.Count > 1 ? cells[1].Trim() : string.Empty;

                if (!known.Contains(key)) { errors.Add($"line {lineNo}: unknown key '{key}'"); continue; }
                if (values.ContainsKey(key)) { errors.Add($"line {lineNo}: duplicate key '{key}' (first on line {lineOf[key]})"); continue; }
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v < 0)
                {
                    errors.Add($"line {lineNo} ({key}): value must be a whole number 0 or more, got '{raw}'");
                    continue;
                }

                values[key] = v;
                lineOf[key] = lineNo;
            }

            foreach (var key in Rules.Keys)
            {
                if (!values.ContainsKey(key)) errors.Add($"line {lines.Count}: missing key '{key}'");
            }

            if (errors.Count == 0)
            {
                void Check(bool ok, string key, string message)
                {
                    if (!ok) errors.Add($"line {lineOf[key]} ({key}): {message}");
                }

                Check(values["hp_max"] > 0, "hp_max", "must be at least 1");
                Check(values["stamina_max"] > 0, "stamina_max", "must be at least 1");
                Check(values["exhausted_until"] <= values["stamina_max"], "exhausted_until",
                    $"({values["exhausted_until"]}) cannot be more than 'stamina_max' ({values["stamina_max"]})");
                Check(values["qi_start"] <= values["qi_max"], "qi_start",
                    $"({values["qi_start"]}) cannot be more than 'qi_max' ({values["qi_max"]})");
                Check(values["input_buffer"] <= 30, "input_buffer", "must be 30 frames or fewer (half a second), or presses fire long after the player meant them");
                Check(values["exhausted_damage_pct"] >= 100, "exhausted_damage_pct", "must be 100 or more (100 = normal damage)");
            }

            return new Result(errors.Count == 0 ? new Rules(values) : null, errors);
        }
    }
}
