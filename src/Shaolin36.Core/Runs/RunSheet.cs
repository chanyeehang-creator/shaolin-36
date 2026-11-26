using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Runs
{
    /// <summary>
    /// The run sheet (game/data/run.csv): the numbers behind the demo roguelike run (Chan's test run). How many fights,
    /// the player's starting and capped stats, what each reward gives, how the enemies grow and which brain each fight uses.
    /// </summary>
    public sealed class RunSheet
    {
        /// <summary>Every whole-number key the sheet must have, in sheet order.</summary>
        public static readonly string[] NumberKeys =
        {
            "fights", "player_hp", "player_stamina", "player_qi",
            "hp_up", "hp_cap", "stamina_up", "stamina_cap", "qi_up", "qi_cap",
            "upgrade_max", "upgrade_damage_pct", "upgrade_stamina_pct",
            "win_heal_pct",
            "rarity_common", "rarity_uncommon", "rarity_rare", "super_rare_from", "super_rare_to", "super_rare_pct",
            "skill_choices", "scroll_picks", "miniboss_hp_bonus",
            "enemy_hp", "enemy_hp_step", "boss_hp", "enemy_stamina", "enemy_qi", "enemy_qi_start", "enemy_moves_max", "enemy_tiger_from",
            "miniboss_opening", "boss_opening",
        };

        /// <summary>Every text key the sheet must have: technique and brain ids, separated by spaces.</summary>
        public static readonly string[] TextKeys =
        {
            "starters", "enemy_first", "enemy_waigong", "brains", "first_doors_a", "first_doors_b", "three_doors", "miniboss_brain",
        };

        private readonly Dictionary<string, int> _numbers;
        private readonly Dictionary<string, string[]> _texts;

        internal RunSheet(Dictionary<string, int> numbers, Dictionary<string, string[]> texts)
        {
            _numbers = numbers;
            _texts = texts;
        }

        /// <summary>A sheet from code (tests): every key of <see cref="NumberKeys"/> and <see cref="TextKeys"/> must be given.</summary>
        public static RunSheet Of(IDictionary<string, int> numbers, IDictionary<string, string> texts)
        {
            var n = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var k in NumberKeys) n[k] = numbers[k];
            var t = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var k in TextKeys) t[k] = Split(texts[k]);
            return new RunSheet(n, t);
        }

        internal static string[] Split(string raw) =>
            raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private int N(string key) => _numbers[key];

        /// <summary>Fights in a run; the last is the boss.</summary>
        public int Fights => N("fights");
        public int PlayerHp => N("player_hp");
        public int PlayerStamina => N("player_stamina");
        public int PlayerQi => N("player_qi");
        public int HpUp => N("hp_up");
        public int HpCap => N("hp_cap");
        public int StaminaUp => N("stamina_up");
        public int StaminaCap => N("stamina_cap");
        public int QiUp => N("qi_up");
        public int QiCap => N("qi_cap");
        /// <summary>Power-ups one move can have in all (damage and stamina together).</summary>
        public int UpgradeMax => N("upgrade_max");
        /// <summary>Damage each "deal more damage" power-up adds, in percent of the move's base damage.</summary>
        public int UpgradeDamagePct => N("upgrade_damage_pct");
        /// <summary>Stamina each "reduce stamina cost" power-up takes off, in percent of the move's base cost.</summary>
        public int UpgradeStaminaPct => N("upgrade_stamina_pct");
        /// <summary>HP healed after each win, in percent of max HP.</summary>
        public int WinHealPct => N("win_heal_pct");
        /// <summary>Chance out of 100 that a door is common (Medicine or Skill Enhance).</summary>
        public int RarityCommon => N("rarity_common");
        /// <summary>Chance out of 100 that a door is uncommon (a new category).</summary>
        public int RarityUncommon => N("rarity_uncommon");
        /// <summary>Chance out of 100 that a door is rare (another move from a category you know).</summary>
        public int RarityRare => N("rarity_rare");
        /// <summary>The first chamber a super rare door (Elixirs or Ancient Scroll, a mini boss) can lead to.</summary>
        public int SuperRareFrom => N("super_rare_from");
        /// <summary>The last chamber it can lead to: if it hasn't shown up before, it always does here.</summary>
        public int SuperRareTo => N("super_rare_to");
        /// <summary>Chance out of 100 it shows up among the doors to each chamber before <see cref="SuperRareTo"/>.</summary>
        public int SuperRarePct => N("super_rare_pct");
        /// <summary>New moves offered after a category door (the player learns one).</summary>
        public int SkillChoices => N("skill_choices");
        /// <summary>Moves the Ancient Scroll powers up (each a different one).</summary>
        public int ScrollPicks => N("scroll_picks");
        /// <summary>A mini boss has this much more HP than that chamber's enemy.</summary>
        public int MinibossHpBonus => N("miniboss_hp_bonus");
        /// <summary>The first enemy's HP.</summary>
        public int EnemyHp => N("enemy_hp");
        /// <summary>HP each later enemy adds.</summary>
        public int EnemyHpStep => N("enemy_hp_step");
        /// <summary>The last fight's enemy (the boss) has this HP instead.</summary>
        public int BossHp => N("boss_hp");

        /// <summary>Frames after each of its attacks in which a mini boss's brain won't parry, clash, guard or dodge: your opening (Chan).</summary>
        public int MinibossOpening => N("miniboss_opening");

        /// <summary>The same opening for the boss (chamber 10). 0 = none.</summary>
        public int BossOpening => N("boss_opening");
        public int EnemyStamina => N("enemy_stamina");
        public int EnemyQi => N("enemy_qi");
        public int EnemyQiStart => N("enemy_qi_start");
        /// <summary>Enemy N knows N moves, up to this many (8 = every button).</summary>
        public int EnemyMovesMax => N("enemy_moves_max");
        /// <summary>From this fight on, the enemy's 外功 is any of <see cref="EnemyWaigong"/>; before it, <see cref="EnemyFirst"/>.</summary>
        public int EnemyTigerFrom => N("enemy_tiger_from");

        /// <summary>The 外功 moves the player chooses between at the start.</summary>
        public IReadOnlyList<string> Starters => _texts["starters"];
        /// <summary>The 外功 the early enemies know.</summary>
        public string EnemyFirst => _texts["enemy_first"].FirstOrDefault() ?? "";
        /// <summary>The 外功 moves later enemies pick from.</summary>
        public IReadOnlyList<string> EnemyWaigong => _texts["enemy_waigong"];
        /// <summary>The brain id for each fight, in order.</summary>
        public IReadOnlyList<string> Brains => _texts["brains"];
        /// <summary>The mini boss's brain id.</summary>
        public string MinibossBrain => _texts["miniboss_brain"].FirstOrDefault() ?? "";

        /// <summary>The categories the first door to chamber 1 is rolled from (the defensive one).</summary>
        public IReadOnlyList<Category> FirstDoorsA => Categories("first_doors_a");
        /// <summary>The categories the second door to chamber 1 is rolled from.</summary>
        public IReadOnlyList<Category> FirstDoorsB => Categories("first_doors_b");

        /// <summary>The chambers you choose from 3 doors to get into (2 elsewhere; 1, the boss's, into the last).</summary>
        public IReadOnlyList<int> ThreeDoors =>
            _texts["three_doors"].Select(t => int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1).ToList();

        private IReadOnlyList<Category> Categories(string key) =>
            _texts[key].Select(t => Enum.TryParse<Category>(t, true, out var c) ? (Category?)c : null).Where(c => c != null).Select(c => c!.Value).ToList();

        /// <summary>
        /// Problems with this sheet against the techniques and brains it names: unknown ids, a brain list that doesn't match the
        /// number of fights, caps below the starting stats. Empty when it's fine.
        /// </summary>
        public List<string> Check(TechniqueBook book, BrainSheet brains)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (brains == null) throw new ArgumentNullException(nameof(brains));
            var errors = new List<string>();
            void Ids(string key, IEnumerable<string> ids)
            {
                foreach (var id in ids)
                    if (book.Find(id) == null) errors.Add($"{key}: no technique '{id}' in techniques.csv");
            }
            Ids("starters", Starters);
            Ids("enemy_first", new[] { EnemyFirst });
            Ids("enemy_waigong", EnemyWaigong);
            if (Starters.Count == 0) errors.Add("starters: name at least one technique");
            if (EnemyWaigong.Count == 0) errors.Add("enemy_waigong: name at least one technique");
            foreach (var id in Brains)
                if (brains.Find(id) == null) errors.Add($"brains: no brain '{id}' in brains.csv");
            if (brains.Find(MinibossBrain) == null) errors.Add($"miniboss_brain: no brain '{MinibossBrain}' in brains.csv");
            foreach (var key in new[] { "first_doors_a", "first_doors_b" })
            {
                foreach (var t in _texts[key])
                    if (!Enum.TryParse<Category>(t, true, out _)) errors.Add($"{key}: '{t}' is not a category (waigong neigong qinggong dianxue fangshen qinna lianqi)");
                if (_texts[key].Length == 0) errors.Add($"{key}: name at least one category");
            }
            foreach (var n in ThreeDoors)
                if (n < 1 || n > Fights) errors.Add($"three_doors: chambers are 1 to {Fights}");
            if (RarityCommon + RarityUncommon + RarityRare != 100) errors.Add("rarity_common + rarity_uncommon + rarity_rare: must add up to 100");
            if (SuperRarePct > 100) errors.Add("super_rare_pct: at most 100");
            if (SuperRareFrom < 2 || SuperRareTo < SuperRareFrom || SuperRareTo >= Fights) errors.Add($"super_rare_from / super_rare_to: between 2 and {Fights - 1}, from no later than to");
            if (SkillChoices < 1) errors.Add("skill_choices: at least 1");
            if (ScrollPicks < 1) errors.Add("scroll_picks: at least 1");
            if (Fights < 1) errors.Add("fights: at least 1");
            if (Brains.Count != Fights) errors.Add($"brains: name one brain per fight ({Fights}), got {Brains.Count}");
            if (PlayerHp < 1) errors.Add("player_hp: at least 1");
            if (PlayerStamina < 1) errors.Add("player_stamina: at least 1");
            if (HpCap < PlayerHp) errors.Add("hp_cap: below player_hp");
            if (StaminaCap < PlayerStamina) errors.Add("stamina_cap: below player_stamina");
            if (QiCap < PlayerQi) errors.Add("qi_cap: below player_qi");
            if (WinHealPct > 100) errors.Add("win_heal_pct: at most 100");
            if (UpgradeStaminaPct * UpgradeMax > 100) errors.Add("upgrade_stamina_pct x upgrade_max: more than 100% off");
            if (EnemyHp < 1 || BossHp < 1) errors.Add("enemy_hp and boss_hp: at least 1");
            if (MinibossOpening > 300 || BossOpening > 300) errors.Add("miniboss_opening and boss_opening: 300 frames (5 s) or fewer");
            if (EnemyStamina < 1) errors.Add("enemy_stamina: at least 1");
            if (EnemyQiStart > EnemyQi) errors.Add("enemy_qi_start: above enemy_qi");
            if (EnemyMovesMax < 1 || EnemyMovesMax > Fighters.Slots.Count) errors.Add($"enemy_moves_max: 1 to {Fighters.Slots.Count}");
            return errors;
        }
    }

    /// <summary>Reads the run sheet: a header 'key,value,note', then one row per key.</summary>
    public static class RunCsv
    {
        public sealed class Result
        {
            public Result(RunSheet? sheet, List<string> errors)
            {
                Sheet = sheet;
                Errors = errors;
            }

            /// <summary>Null when there are errors.</summary>
            public RunSheet? Sheet { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var errors = new List<string>();
            var lines = CsvText.SplitLines(text);
            var numberKeys = new HashSet<string>(RunSheet.NumberKeys, StringComparer.Ordinal);
            var textKeys = new HashSet<string>(RunSheet.TextKeys, StringComparer.Ordinal);
            var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
            var texts = new Dictionary<string, string[]>(StringComparer.Ordinal);
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
                if (lineOf.ContainsKey(key)) { errors.Add($"line {lineNo}: duplicate key '{key}' (first on line {lineOf[key]})"); continue; }
                if (numberKeys.Contains(key))
                {
                    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v < 0)
                    {
                        errors.Add($"line {lineNo} ({key}): value must be a whole number 0 or more, got '{raw}'");
                        continue;
                    }
                    numbers[key] = v;
                }
                else if (textKeys.Contains(key))
                {
                    texts[key] = RunSheet.Split(raw);
                }
                else
                {
                    errors.Add($"line {lineNo}: unknown key '{key}'");
                    continue;
                }
                lineOf[key] = lineNo;
            }

            foreach (var key in RunSheet.NumberKeys.Concat(RunSheet.TextKeys))
                if (!lineOf.ContainsKey(key)) errors.Add($"missing key '{key}'");

            return errors.Count == 0 ? new Result(new RunSheet(numbers, texts), errors) : new Result(null, errors);
        }
    }
}
