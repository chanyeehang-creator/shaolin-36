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
            "exhausted_until", "exhausted_damage_pct", "exhausted_walk_pct", "qi_max", "qi_start", "qi_per_hit",
            "clash_recoil_frames", "clash_recoil_stamina", "clash_lose_stagger", "clash_lose_stamina",
            "clash_perfect_stagger", "clash_perfect_recovery", "clash_perfect_qi", "clash_recoil_push",
            "parry_recovery", "parry_perfect_recovery", "parry_perfect_stagger", "parry_perfect_drain", "parry_perfect_qi",
            "input_buffer", "knockback_frames",
            "walk_speed", "min_distance", "start_distance", "arena_width",
            "dummy_pause", "dummy_chain_frame",
            "quick_step_distance", "quick_step_frames", "quick_step_recovery", "quick_step_stamina", "quick_step_tap_window",
            "quick_step_side_distance", "opponent_ready", "just_dodge_qi",
            "down_min", "down_max", "rise_frames", "roll_frames", "roll_distance",
            "block_stamina_light", "block_stamina_medium", "block_stamina_heavy", "block_heavy_damage_pct",
            "cripple_walk_pct", "grab_strike_gap", "grab_break_recoil", "grab_break_push", "relock_turn_frames", "quick_step_invincible",
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
        /// <summary>Walking speed while exhausted, as a percentage of walk_speed (50 = half speed).</summary>
        public int ExhaustedWalkPct => V("exhausted_walk_pct");
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
        /// <summary>Same-weight clash: each fighter is pushed back this many centimetres.</summary>
        public int ClashRecoilPush => V("clash_recoil_push");

        /// <summary>Knockback (and clash push) slides the fighter back over this many frames.</summary>
        public int KnockbackFrames => V("knockback_frames");

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

        /// <summary>Walking speed in centimetres per second. Fighters only walk while idle.</summary>
        public int WalkSpeed => V("walk_speed");

        /// <summary>Fighters can't walk closer together than this, in centimetres.</summary>
        public int MinDistance => V("min_distance");

        /// <summary>How far apart the two fighters start a duel, in centimetres.</summary>
        public int StartDistance => V("start_distance");

        /// <summary>Width of the fight line, in centimetres, centred on 0. Fighters can't walk past its ends.</summary>
        public int ArenaWidth => V("arena_width");

        /// <summary>Playtest dummy: idle frames it waits between strings.</summary>
        public int DummyPause => V("dummy_pause");

        /// <summary>
        /// Playtest dummy: which chain-window frame it presses on to chain the next hit (0 = the first,
        /// a true combo). At or past a technique's chain window it never chains.
        /// </summary>
        public int DummyChainFrame => V("dummy_chain_frame");

        /// <summary>How far a forward or back quick step goes, in centimetres.</summary>
        public int QuickStepDistance => V("quick_step_distance");

        /// <summary>How far a left or right quick step goes, in centimetres.</summary>
        public int QuickStepSideDistance => V("quick_step_side_distance");
        /// <summary>Quick step: its first this many frames are invincible (never more than quick_step_frames: the recovery never is).</summary>
        public int QuickStepInvincible => V("quick_step_invincible");

        /// <summary>Frames a quick step slides over.</summary>
        public int QuickStepFrames => V("quick_step_frames");

        /// <summary>Frames after a quick step's slide before the fighter is free again.</summary>
        public int QuickStepRecovery => V("quick_step_recovery");

        /// <summary>Stamina a quick step costs.</summary>
        public int QuickStepStamina => V("quick_step_stamina");

        /// <summary>Frames the opponent brain waits at the start of a duel before it does anything.</summary>
        public int OpponentReady => V("opponent_ready");

        /// <summary>Qi for a just dodge: a strike passing through a dash's invincible frames (not one started out of hitstun).</summary>
        public int JustDodgeQi => V("just_dodge_qi");

        /// <summary>Knocked down: frames on the floor before you can rise or roll.</summary>
        public int DownMin => V("down_min");

        /// <summary>Knocked down: after this many frames on the floor you rise by yourself (unless exhausted).</summary>
        public int DownMax => V("down_max");

        /// <summary>Frames a quick rise takes (invincible) before you're free.</summary>
        public int RiseFrames => V("rise_frames");

        /// <summary>Frames a roll away takes (invincible) before you're free.</summary>
        public int RollFrames => V("roll_frames");

        /// <summary>How far a roll away goes, in centimetres.</summary>
        public int RollDistance => V("roll_distance");

        /// <summary>硬气功 guard: stamina a blocked light strike costs the guard.</summary>
        public int BlockStaminaLight => V("block_stamina_light");

        /// <summary>硬气功 guard: stamina a blocked medium strike costs the guard.</summary>
        public int BlockStaminaMedium => V("block_stamina_medium");

        /// <summary>硬气功 guard: stamina a blocked heavy or energy strike costs the guard.</summary>
        public int BlockStaminaHeavy => V("block_stamina_heavy");

        /// <summary>硬气功 guard: damage a heavy or energy strike still deals through it, as a percentage (50 = half).</summary>
        public int BlockHeavyDamagePct => V("block_heavy_damage_pct");

        /// <summary>Stamina a blocked strike of <paramref name="weight"/> costs a 硬气功 guard.</summary>
        public int BlockStamina(Weight weight) =>
            weight == Weight.Light ? BlockStaminaLight : weight == Weight.Medium ? BlockStaminaMedium : BlockStaminaHeavy;

        /// <summary>无相劫指's movement lock: walking speed while crippled, as a percentage of walk_speed.</summary>
        public int CrippleWalkPct => V("cripple_walk_pct");

        /// <summary>龙爪手: the fewest frames between two strikes of a multi-strike grab (taps sooner wait in the input buffer).</summary>
        public int GrabStrikeGap => V("grab_strike_gap");

        /// <summary>Grab break-out: frames both fighters recoil when the held one breaks free with a grab of their own.</summary>
        public int GrabBreakRecoil => V("grab_break_recoil");

        /// <summary>Grab break-out: how far each fighter is pushed apart, in centimetres.</summary>
        public int GrabBreakPush => V("grab_break_push");

        /// <summary>After finding a lost opponent again (a 纵云梯 over their head), frames a half turn back round takes.</summary>
        public int RelockTurnFrames => V("relock_turn_frames");

        /// <summary>The most frames from the first tap of a direction to the second that still makes a quick step.</summary>
        public int QuickStepTapWindow => V("quick_step_tap_window");
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
                Check(values["knockback_frames"] >= 1, "knockback_frames", "must be at least 1 frame");
                Check(values["walk_speed"] <= 2000, "walk_speed", "must be 2000 cm per second or less");
                Check(values["min_distance"] > 0, "min_distance", "must be at least 1, or the fighters walk through each other");
                Check(values["start_distance"] >= values["min_distance"], "start_distance",
                    $"({values["start_distance"]}) cannot be less than 'min_distance' ({values["min_distance"]})");
                Check(values["start_distance"] <= values["arena_width"], "start_distance",
                    $"({values["start_distance"]}) cannot be more than 'arena_width' ({values["arena_width"]})");
                Check(values["exhausted_walk_pct"] <= 100, "exhausted_walk_pct", "must be 100 or less (100 = normal speed)");
                Check(values["exhausted_damage_pct"] >= 100, "exhausted_damage_pct", "must be 100 or more (100 = normal damage)");
                Check(values["quick_step_distance"] <= 300, "quick_step_distance", "must be 300 cm or less: it's a step, not a dash");
                Check(values["quick_step_side_distance"] <= 300, "quick_step_side_distance", "must be 300 cm or less: it's a step, not a dash");
                Check(values["opponent_ready"] <= 600, "opponent_ready", "must be 600 frames (10 s) or fewer");
                Check(values["down_max"] >= values["down_min"], "down_max", $"({values["down_max"]}) cannot be less than 'down_min' ({values["down_min"]})");
                Check(values["rise_frames"] >= 1, "rise_frames", "must be at least 1 frame");
                Check(values["roll_frames"] >= 1, "roll_frames", "must be at least 1 frame");
                Check(values["roll_distance"] <= 600, "roll_distance", "must be 600 cm or less");
                Check(values["block_heavy_damage_pct"] <= 100, "block_heavy_damage_pct", "must be 100 or less (100 = the guard doesn't soften heavy hits)");
                Check(values["cripple_walk_pct"] <= 100, "cripple_walk_pct", "must be 100 or less (100 = normal speed)");
                Check(values["relock_turn_frames"] >= 1 && values["relock_turn_frames"] <= 120, "relock_turn_frames", "must be between 1 and 120 frames");
                Check(values["grab_break_recoil"] >= 1, "grab_break_recoil", "must be at least 1 frame");
                Check(values["grab_break_push"] <= 300, "grab_break_push", "must be 300 cm or less");
                Check(values["quick_step_frames"] >= 1, "quick_step_frames", "must be at least 1 frame");
                Check(values["quick_step_invincible"] <= values["quick_step_frames"], "quick_step_invincible",
                    $"({values["quick_step_invincible"]}) cannot be more than 'quick_step_frames' ({values["quick_step_frames"]}): the recovery is never invincible");
                Check(values["quick_step_tap_window"] >= 1 && values["quick_step_tap_window"] <= 30, "quick_step_tap_window",
                    "must be between 1 and 30 frames (half a second)");
            }

            return new Result(errors.Count == 0 ? new Rules(values) : null, errors);
        }
    }
}
