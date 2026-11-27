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
        public static readonly string[] OptionalColumns = { "empower_qi", "empower_damage_pct", "empower_armor", "knockback", "hit_angle", "advance", "dash", "invincible", "escape", "seal", "heal", "lunge", "hold", "knockdown", "hits_down", "early_block", "hit_recovery", "jump", "cripple", "auto_chain", "guard", "grab_hits", "buff", "buff_damage_pct", "lunge_stops", "recovery_advance" };

        /// <summary>Most a hit can push the defender back, in centimetres.</summary>
        public const int MaxKnockback = 500;

        /// <summary>Most a move can step the attacker forward, in centimetres.</summary>
        public const int MaxAdvance = 300;

        /// <summary>Most a 轻功 dash can carry the fighter, in centimetres.</summary>
        public const int MaxDash = 600;

        /// <summary>Longest 点穴 seal, in frames (30 s).</summary>
        public const int MaxSeal = 1800;

        /// <summary>Longest 擒拿 hold before the throw, in frames (5 s).</summary>
        public const int MaxHold = 300;

        /// <summary>Most HP one heal can restore.</summary>
        public const int MaxHeal = 1000;

        private static readonly Dictionary<string, Category> CategoryNames =
            new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
            {
                ["waigong"] = Category.Waigong, ["外功"] = Category.Waigong,
                ["neigong"] = Category.Neigong, ["内功"] = Category.Neigong,
                ["qinggong"] = Category.Qinggong, ["轻功"] = Category.Qinggong,
                ["dianxue"] = Category.Dianxue, ["点穴"] = Category.Dianxue,
                ["fangshen"] = Category.Fangshen, ["防身"] = Category.Fangshen,
                ["huajin"] = Category.Fangshen, ["化劲"] = Category.Fangshen,
                ["qinna"] = Category.Qinna, ["擒拿"] = Category.Qinna,
                ["lianqi"] = Category.Lianqi, ["炼气"] = Category.Lianqi,
                ["qigong"] = Category.Lianqi, ["气功"] = Category.Lianqi,
            };

        private static readonly Dictionary<string, Weight> WeightNames =
            new Dictionary<string, Weight>(StringComparer.OrdinalIgnoreCase)
            {
                ["light"] = Weight.Light, ["轻"] = Weight.Light,
                ["medium"] = Weight.Medium, ["中"] = Weight.Medium,
                ["heavy"] = Weight.Heavy, ["重"] = Weight.Heavy,
                ["energy"] = Weight.Energy, ["气"] = Weight.Energy,
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
                    rowErrors.Add($"unknown weight '{Cell("weight")}' (light, medium, heavy or energy)");

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
                int advance = Cell("advance").Length == 0 ? 0 : Int("advance");
                int dash = Cell("dash").Length == 0 ? 0 : Int("dash");
                int invincible = Cell("invincible").Length == 0 ? 0 : Int("invincible");
                bool escape = Cell("escape").Length != 0 && Bool("escape");
                int seal = Cell("seal").Length == 0 ? 0 : Int("seal");
                int heal = Cell("heal").Length == 0 ? 0 : Int("heal");
                int lunge = Cell("lunge").Length == 0 ? 0 : Int("lunge");
                bool lungeStops = Cell("lunge_stops").Length != 0 && Bool("lunge_stops");
                int recoveryAdvance = Cell("recovery_advance").Length == 0 ? 0 : Int("recovery_advance");
                int hold = Cell("hold").Length == 0 ? 0 : Int("hold");
                bool knockdown = Cell("knockdown").Length != 0 && Bool("knockdown");
                bool hitsDown = Cell("hits_down").Length != 0 && Bool("hits_down");
                int earlyBlock = Cell("early_block").Length == 0 ? 0 : Int("early_block");
                int hitRecovery = Cell("hit_recovery").Length == 0 ? 0 : Int("hit_recovery");
                bool jump = Cell("jump").Length != 0 && Bool("jump");
                int cripple = Cell("cripple").Length == 0 ? 0 : Int("cripple");
                bool autoChain = Cell("auto_chain").Length != 0 && Bool("auto_chain");
                bool guard = Cell("guard").Length != 0 && Bool("guard");
                int grabHits = Cell("grab_hits").Length == 0 ? 0 : Int("grab_hits");
                int buff = Cell("buff").Length == 0 ? 0 : Int("buff");
                int buffPct = Cell("buff_damage_pct").Length == 0 ? 100 : Int("buff_damage_pct");

                if (startup < 1) rowErrors.Add("'startup' must be at least 1 frame");
                if (active < 1) rowErrors.Add("'active' must be at least 1 frame");
                if (window > startup) rowErrors.Add($"'clash_window' ({window}) cannot be longer than 'startup' ({startup})");
                if (perfect > window) rowErrors.Add($"'perfect_clash' ({perfect}) cannot be longer than 'clash_window' ({window})");
                if (!clashable && window > 0) rowErrors.Add("an unclashable technique must have 'clash_window' 0");
                if (chain > recovery) rowErrors.Add($"'chain_window' ({chain}) cannot be longer than 'recovery' ({recovery})");
                if (next.Length > 0 && chain == 0) rowErrors.Add($"'next' is '{next}' but 'chain_window' is 0, so the string can never continue");
                if (next.Length == 0 && chain > 0) rowErrors.Add("'chain_window' is set but 'next' is empty");
                if (next.Length > 0 && next == id) rowErrors.Add("'next' cannot be the technique itself");
                if (perfectParry > 0 && category != Category.Fangshen) rowErrors.Add("only 防身 (fangshen) techniques can have 'perfect_parry'");
                if (perfectParry > active) rowErrors.Add($"'perfect_parry' ({perfectParry}) cannot be longer than 'active' ({active}), which is the parry window");
                if (earlyBlock > 0 && perfectParry == 0) rowErrors.Add("'early_block' is set but 'perfect_parry' is 0: only a 防身 (fangshen) parry has a block window");
                if (earlyBlock + perfectParry > active)
                    rowErrors.Add($"'early_block' + 'perfect_parry' ({earlyBlock} + {perfectParry}) cannot be longer than 'active' ({active}), which is the parry window");
                if (hitRecovery > 0 && reach == 0) rowErrors.Add("'hit_recovery' is set but 'reach' is 0, so the move never hits");
                if (hitRecovery > 0 && hitRecovery >= active + recovery)
                    rowErrors.Add($"'hit_recovery' ({hitRecovery}) must be shorter than 'active' + 'recovery' ({active + recovery}), or it isn't a shorter recovery");

                if (empowerQi == 0 && (empowerPct != 100 || empowerArmor))
                    rowErrors.Add("'empower_damage_pct' or 'empower_armor' is set but 'empower_qi' is 0, so the empowered version can never happen");
                if (knockback > MaxKnockback) rowErrors.Add($"'knockback' ({knockback}) must be {MaxKnockback} cm or less");
                if (knockback > 0 && reach == 0) rowErrors.Add("'knockback' is set but 'reach' is 0, so the move never hits");
                if (hitAngle < 1 || hitAngle > Technique.FullCircle) rowErrors.Add($"'hit_angle' ({hitAngle}) must be between 1 and {Technique.FullCircle} degrees ({Technique.FullCircle} = all round)");
                if (advance > MaxAdvance) rowErrors.Add($"'advance' ({advance}) must be {MaxAdvance} cm or less");
                if (dash > MaxDash) rowErrors.Add($"'dash' ({dash}) must be {MaxDash} cm or less");
                if (dash > 0 && advance > 0) rowErrors.Add("a move can have 'dash' or 'advance', not both");
                if (dash > 0 && category != Category.Qinggong) rowErrors.Add("only 轻功 (qinggong) techniques can have 'dash'");
                if (escape && category != Category.Qinggong) rowErrors.Add("only 轻功 (qinggong) techniques can have 'escape'");
                if (invincible > startup + active + recovery)
                    rowErrors.Add($"'invincible' ({invincible}) cannot be longer than the whole move ({startup + active + recovery} frames)");
                if (seal > MaxSeal) rowErrors.Add($"'seal' ({seal}) must be {MaxSeal} frames or less");
                if (seal > 0 && category != Category.Dianxue) rowErrors.Add("only 点穴 (dianxue) techniques can have 'seal'");
                if (seal > 0 && reach == 0) rowErrors.Add("'seal' is set but 'reach' is 0, so the move never hits");
                if (lunge > MaxDash) rowErrors.Add($"'lunge' ({lunge}) must be {MaxDash} cm or less");
                if (lunge > 0 && reach == 0) rowErrors.Add("'lunge' is set but 'reach' is 0, so the move never hits");
                if (lunge > 0 && dash > 0) rowErrors.Add("a move can have 'dash' or 'lunge', not both");
                if (heal > MaxHeal) rowErrors.Add($"'heal' ({heal}) must be {MaxHeal} HP or less");
                if (heal > 0 && category != Category.Lianqi) rowErrors.Add("only 炼气 (lianqi) techniques can have 'heal'");
                if (heal > 0 && recovery == 0) rowErrors.Add("a heal lands on its first recovery frame, so it needs 'recovery' 1 or more");
                if (heal > 0 && reach > 0) rowErrors.Add("a heal can't also be a strike: 'heal' needs 'reach' 0");
                if (hold > MaxHold) rowErrors.Add($"'hold' ({hold}) must be {MaxHold} frames or less");
                if (hold > 0 && category != Category.Qinna) rowErrors.Add("only 擒拿 (qinna) techniques can have 'hold'");
                if (hold == 0 && category == Category.Qinna)
                    rowErrors.Add("a 擒拿 (qinna) technique is a grab: it needs 'hold' 1 or more");
                if (hold > 0 && reach == 0) rowErrors.Add("'hold' is set but 'reach' is 0, so the grab never catches");
                if (hold > 0 && clashable) rowErrors.Add("a grab can't be clashed: it needs 'clashable' false");
                if (hold > 0 && hitstun == 0 && !knockdown) rowErrors.Add("a grab's throw needs 'hitstun' 1 or more, or 'knockdown' true");
                if (knockdown && reach == 0) rowErrors.Add("'knockdown' is set but 'reach' is 0, so the move never hits");
                if (hitsDown && reach == 0) rowErrors.Add("'hits_down' is set but 'reach' is 0, so the move never hits");
                if (hitsDown && hold > 0) rowErrors.Add("a grab can't catch a downed fighter: leave 'hits_down' false");
                if (hold > 0 && lunge > 0) rowErrors.Add("a grab can't have 'lunge' yet (use 'advance' to step in)");
                if (recoveryAdvance > MaxAdvance) rowErrors.Add($"'recovery_advance' ({recoveryAdvance}) must be {MaxAdvance} cm or less");
                if (recoveryAdvance > 0 && recovery == 0) rowErrors.Add("'recovery_advance' is set but 'recovery' is 0, so there's no recovery to move in");
                if (lungeStops && lunge == 0) rowErrors.Add("'lunge_stops' is set but 'lunge' is 0, so there's no lunge to stop");
                if (hold > 0 && empowerQi > 0) rowErrors.Add("a grab has no empowered version yet: leave 'empower_qi' 0");
                if (empowerQi > 0 && empowerPct == 0) rowErrors.Add("'empower_damage_pct' must be above 0");
                if (jump && dash == 0) rowErrors.Add("'jump' is set but 'dash' is 0: a jump is a dash that leaps, so it needs a distance");
                if (jump && escape) rowErrors.Add("a jump can't 'escape' a combo");
                if (cripple > MaxSeal) rowErrors.Add($"'cripple' ({cripple}) must be {MaxSeal} frames or less");
                if (cripple > 0 && category != Category.Dianxue) rowErrors.Add("only 点穴 (dianxue) techniques can have 'cripple'");
                if (cripple > 0 && reach == 0) rowErrors.Add("'cripple' is set but 'reach' is 0, so the move never hits");
                if (autoChain && next.Length == 0) rowErrors.Add("'auto_chain' is set but 'next' is empty");
                if (guard && category != Category.Fangshen) rowErrors.Add("only 防身 (fangshen) techniques can 'guard'");
                if (guard && perfectParry > 0) rowErrors.Add("a guard has no parry: leave 'perfect_parry' 0");
                if (guard && reach > 0) rowErrors.Add("a guard can't also be a strike: 'guard' needs 'reach' 0");
                if (grabHits > 10) rowErrors.Add($"'grab_hits' ({grabHits}) must be 10 or fewer");
                if (grabHits > 0 && hold == 0) rowErrors.Add("'grab_hits' is set but 'hold' is 0: only a grab can strike while it holds");
                if (buff > MaxSeal) rowErrors.Add($"'buff' ({buff}) must be {MaxSeal} frames or less");
                if (buff > 0 && category != Category.Lianqi) rowErrors.Add("only 炼气 (lianqi) techniques can have 'buff'");
                if (buff > 0 && recovery == 0) rowErrors.Add("a buff lands on its first recovery frame, so it needs 'recovery' 1 or more");
                if (buff > 0 && reach > 0) rowErrors.Add("a buff can't also be a strike: 'buff' needs 'reach' 0");
                if (buff == 0 && buffPct != 100) rowErrors.Add("'buff_damage_pct' is set but 'buff' is 0, so the buff never happens");
                if (buff > 0 && (buffPct == 0 || buffPct > 1000)) rowErrors.Add("'buff_damage_pct' must be between 1 and 1000");
                if (weight == Weight.Energy && weapon) rowErrors.Add("a pure energy move can't be a weapon");

                if (rowErrors.Count > 0)
                {
                    foreach (var e in rowErrors) errors.Add($"line {lineNo} ({(id.Length > 0 ? id : "?")}): {e}");
                    continue;
                }

                if (next.Length > 0) chains.Add((lineNo, id, next));
                techniques.Add(new Technique(id, displayName, category, weight, weapon,
                    startup, active, recovery, window, perfect, stamina, qi, damage, clashable,
                    hitstun, reach, next, chain, perfectParry, empowerQi, empowerPct, empowerArmor, knockback, hitAngle, advance, dash, invincible, escape, seal, heal, lunge, hold, knockdown, hitsDown, earlyBlock, hitRecovery,
                    jump, cripple, autoChain, guard, grabHits, buff, buffPct, lungeStops, recoveryAdvance));
            }

            foreach (var (line, id, next) in chains)
            {
                if (!seenIds.Contains(next)) errors.Add($"line {line} ({id}): 'next' is '{next}', but no technique has that id");
            }

            return new Result(techniques, errors);
        }
    }
}
