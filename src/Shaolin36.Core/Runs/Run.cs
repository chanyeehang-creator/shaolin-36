using System;
using System.Collections.Generic;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Runs
{
    /// <summary>Where a run is.</summary>
    public enum RunPhase
    {
        /// <summary>At the start: read one of the scrolls (罗汉拳 or 虎形拳).</summary>
        ChooseStarter,
        /// <summary>A move was just learnt: choose an empty button for it.</summary>
        PlaceMove,
        /// <summary>In a cleared chamber (or chamber 0): walk about, try moves, then go through one of <see cref="Run.Doors"/>.</summary>
        Roam,
        /// <summary>Through a door: ready for, or in, the fight of chamber <see cref="Run.Chamber"/>.</summary>
        Fight,
        /// <summary>Won the chamber: choose from <see cref="Run.Offers"/> (<see cref="Run.PicksLeft"/> more times).</summary>
        Reward,
        /// <summary>Beat the boss: the run is won.</summary>
        Victory,
        /// <summary>Lost a fight: the run is over and starts again from the beginning.</summary>
        Defeat,
    }

    /// <summary>How rare a door is: the chance it is rolled (common 60, uncommon 30, rare 10 on the sheet).</summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        /// <summary>Once a run, leading to a chamber between super_rare_from and super_rare_to: a mini boss.</summary>
        SuperRare,
        /// <summary>The one door into the last chamber.</summary>
        Boss,
    }

    /// <summary>What lies behind a door, and so what winning its chamber gives.</summary>
    public enum DoorKind
    {
        /// <summary>A move from <see cref="Door.Category"/>, a category you don't know yet (uncommon; every door into chamber 1).</summary>
        NewCategory,
        /// <summary>Medicine (common): raise max HP, stamina or Qi.</summary>
        Medicine,
        /// <summary>Skill Enhance (common): power up one move you know.</summary>
        SkillEnhance,
        /// <summary>Another move from <see cref="Door.Category"/>, a category you know (rare).</summary>
        SameCategory,
        /// <summary>Elixirs (super rare, a mini boss): raise two of max HP, stamina and Qi.</summary>
        Elixirs,
        /// <summary>Ancient Scroll (super rare, a mini boss): power up scroll_picks different moves.</summary>
        AncientScroll,
        /// <summary>The boss's chamber, the last.</summary>
        Boss,
    }

    /// <summary>One door out of a cleared chamber.</summary>
    public sealed class Door
    {
        public Door(DoorKind kind, Rarity rarity, Category? category = null)
        {
            Kind = kind;
            Rarity = rarity;
            Category = category;
        }

        public DoorKind Kind { get; }
        public Rarity Rarity { get; }

        /// <summary>For <see cref="DoorKind.NewCategory"/> and <see cref="DoorKind.SameCategory"/>: which category's move it teaches.</summary>
        public Category? Category { get; }

        /// <summary>True for the doors to a mini boss.</summary>
        public bool IsMiniboss => Rarity == Rarity.SuperRare;
    }

    /// <summary>The kinds of reward to choose from after a win.</summary>
    public enum OfferKind
    {
        /// <summary>Power up <see cref="Offer.Move"/> (<see cref="Offer.Upgrade"/> says how).</summary>
        Upgrade,
        /// <summary>Raise max HP, stamina and/or Qi by <see cref="Offer.Hp"/>, <see cref="Offer.Stamina"/> and <see cref="Offer.Qi"/>.</summary>
        Stats,
        /// <summary>Learn <see cref="Offer.Move"/> (it then goes on an empty button).</summary>
        NewMove,
    }

    /// <summary>The two power-ups a move can get in the demo run.</summary>
    public enum UpgradeKind
    {
        /// <summary>Every hit of its string deals more damage.</summary>
        Damage,
        /// <summary>Every hit of its string costs less stamina.</summary>
        Stamina,
    }

    /// <summary>One reward on offer after a win.</summary>
    public sealed class Offer
    {
        public Offer(OfferKind kind, Technique? move = null, UpgradeKind upgrade = UpgradeKind.Damage, int hp = 0, int stamina = 0, int qi = 0)
        {
            Kind = kind;
            Move = move;
            Upgrade = upgrade;
            Hp = hp;
            Stamina = stamina;
            Qi = qi;
        }

        public OfferKind Kind { get; }

        /// <summary>The move to power up (<see cref="OfferKind.Upgrade"/>) or to learn (<see cref="OfferKind.NewMove"/>).</summary>
        public Technique? Move { get; }

        /// <summary>For <see cref="OfferKind.Upgrade"/>: which power-up.</summary>
        public UpgradeKind Upgrade { get; }

        /// <summary>For <see cref="OfferKind.Stats"/>: how much max HP goes up.</summary>
        public int Hp { get; }
        /// <summary>For <see cref="OfferKind.Stats"/>: how much max stamina goes up.</summary>
        public int Stamina { get; }
        /// <summary>For <see cref="OfferKind.Stats"/>: how much max Qi goes up.</summary>
        public int Qi { get; }
    }

    /// <summary>Who a chamber's fight is against: its moves on its buttons, its stats and the brain that plays it.</summary>
    public sealed class EnemyPlan
    {
        public EnemyPlan(int number, bool isBoss, bool isMiniboss, Loadout loadout, FighterStats stats, string brainId, int brainSeed,
            int openingFrames = 0, int stepEscapePct = 0)
        {
            OpeningFrames = openingFrames;
            StepEscapePct = stepEscapePct;
            Number = number;
            IsBoss = isBoss;
            IsMiniboss = isMiniboss;
            Loadout = loadout;
            Stats = stats;
            BrainId = brainId;
            BrainSeed = brainSeed;
        }

        /// <summary>Which chamber, from 1.</summary>
        public int Number { get; }
        public bool IsBoss { get; }
        /// <summary>A super rare door's mini boss: more HP and the miniboss_brain.</summary>
        public bool IsMiniboss { get; }
        public Loadout Loadout { get; }
        public FighterStats Stats { get; }
        /// <summary>A row id of brains.csv.</summary>
        public string BrainId { get; }
        /// <summary>The seed for its brain: fixed by the run's seed and the chamber.</summary>
        public int BrainSeed { get; }
        /// <summary>Its brain's <see cref="Brains.Brain.OpeningFrames"/>: miniboss_opening for a mini boss, boss_opening for the boss, else 0.</summary>
        public int OpeningFrames { get; }

        /// <summary>Its brain's <see cref="Brains.Brain.StepEscapePct"/>: miniboss_step_escape for a mini boss, boss_step_escape for the boss, else 0.</summary>
        public int StepEscapePct { get; }
    }

    /// <summary>
    /// The demo roguelike run (Chan's chamber-and-door design). Read one of two 外功 scrolls and put it on a button, then roam
    /// chamber 0 (no enemy) and pick a door. Each door leads to a chamber with a fight, and winning it gives the door's
    /// reward: a move from a new category, Medicine (max HP, stamina or Qi), Skill Enhance (power up a move), another move
    /// from a category you know, or once a run a mini boss with Elixirs or an Ancient Scroll. Then roam the cleared chamber
    /// and pick the next door. Chamber 10 is the boss. Each win heals win_heal_pct of max HP; Qi carries over. Lose once and
    /// the run starts again from scratch.
    ///
    /// Every random choice is rolled on a <see cref="Dice"/> from the run's seed, so the same seed and choices give the same run.
    /// The game asks for <see cref="StartFight"/>'s duel, plays it, and hands it back to <see cref="EndFight"/>.
    /// </summary>
    public sealed class Run
    {
        private readonly Rules _rules;
        private readonly TechniqueBook _book;
        private readonly List<Technique> _pool;
        private readonly List<Technique> _starters;
        private Dice _dice;
        private List<Offer> _offers = new List<Offer>();
        private List<Door> _doors = new List<Door>();

        /// <param name="learnable">Every move a run can teach or give an enemy (moves that sit on a button, not later hits of a string).</param>
        public Run(RunSheet sheet, Rules rules, TechniqueBook book, IEnumerable<Technique> learnable, int seed)
        {
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _book = book ?? throw new ArgumentNullException(nameof(book));
            if (learnable == null) throw new ArgumentNullException(nameof(learnable));
            _pool = learnable.ToList();
            _starters = sheet.Starters.Select(Need).ToList();
            Seed = seed;
            _dice = new Dice(seed);
            Player = new Loadout(book);
            Restart();
        }

        public RunSheet Sheet { get; }

        /// <summary>The seed the current attempt rolls on (it changes on each <see cref="Restart"/>).</summary>
        public int Seed { get; private set; }

        /// <summary>How many times the run was started (1 for the first attempt).</summary>
        public int Attempt { get; private set; }

        public RunPhase Phase { get; private set; }

        /// <summary>The chamber you are in: 0 at the start (no enemy), then 1 to <see cref="RunSheet.Fights"/> (the boss).</summary>
        public int Chamber { get; private set; }

        /// <summary>The player's buttons. A run never replaces a move: new ones go on empty buttons.</summary>
        public Loadout Player { get; private set; }

        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public int MaxStamina { get; private set; }
        public int MaxQi { get; private set; }
        public int Qi { get; private set; }

        /// <summary>HP healed after the last win (for the screen).</summary>
        public int LastHeal { get; private set; }

        /// <summary>True once the super rare door has been shown (it shows once a run, taken or not).</summary>
        public bool SuperRareShown { get; private set; }

        /// <summary>The 外功 scrolls to choose from at the start.</summary>
        public IReadOnlyList<Technique> Starters => _starters;

        /// <summary>The move waiting for a button (<see cref="RunPhase.PlaceMove"/>).</summary>
        public Technique? Pending { get; private set; }

        /// <summary>The doors out of this chamber (<see cref="RunPhase.Roam"/>), leading to chamber <see cref="Chamber"/> + 1.</summary>
        public IReadOnlyList<Door> Doors => _doors;

        /// <summary>The door you came through into this chamber (null in chamber 0).</summary>
        public Door? Door { get; private set; }

        /// <summary>The rewards on offer (<see cref="RunPhase.Reward"/>).</summary>
        public IReadOnlyList<Offer> Offers => _offers;

        /// <summary>How many more rewards to choose in this <see cref="RunPhase.Reward"/> (2 for the Ancient Scroll, else 1).</summary>
        public int PicksLeft { get; private set; }

        /// <summary>This chamber's enemy (<see cref="RunPhase.Fight"/>).</summary>
        public EnemyPlan? Enemy { get; private set; }

        /// <summary>The player's stats for the next fight: HP and Qi carried over, stamina full.</summary>
        public FighterStats PlayerStats => new FighterStats(MaxHp, Hp, MaxStamina, MaxQi, Qi);

        /// <summary>The moves the player has on a button.</summary>
        public IEnumerable<Technique> Learnt => Slots.All.Select(s => Player[s]).Where(t => t != null).Select(t => t!).Distinct();

        /// <summary>True if the player knows <paramref name="t"/>.</summary>
        public bool Knows(Technique t) => Learnt.Contains(t);

        /// <summary>True if the player knows a move of category <paramref name="c"/>.</summary>
        public bool KnowsCategory(Category c) => Learnt.Any(t => t.Category == c);

        public bool HasEmptySlot => Slots.All.Any(s => Player.IsEmpty(s));

        /// <summary>Start again from scratch: base stats, no moves, chamber 0. A new seed, counted on from the first.</summary>
        public void Restart()
        {
            Attempt++;
            if (Attempt > 1)
            {
                Seed = unchecked(Seed * 31 + 7);
                _dice = new Dice(Seed);
            }
            Player = new Loadout(_book);
            MaxHp = Hp = Sheet.PlayerHp;
            MaxStamina = Sheet.PlayerStamina;
            MaxQi = Sheet.PlayerQi;
            Qi = Math.Min(_rules.QiStart, MaxQi);
            Chamber = 0;
            LastHeal = 0;
            SuperRareShown = false;
            Pending = null;
            Enemy = null;
            Door = null;
            _offers = new List<Offer>();
            _doors = new List<Door>();
            PicksLeft = 0;
            Phase = RunPhase.ChooseStarter;
        }

        /// <summary>Read scroll <paramref name="index"/>; next, choose its button.</summary>
        public void ChooseStarter(int index)
        {
            Expect(RunPhase.ChooseStarter);
            if (index < 0 || index >= _starters.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Pending = _starters[index];
            Phase = RunPhase.PlaceMove;
        }

        /// <summary>Put the pending move on <paramref name="slot"/>. False (nothing happens) if that button already has a move.</summary>
        public bool Place(Slot slot)
        {
            Expect(RunPhase.PlaceMove);
            if (!Player.IsEmpty(slot)) return false;
            Player.Assign(slot, Pending!);
            Pending = null;
            StartRoam();
            return true;
        }

        /// <summary>Go through door <paramref name="index"/> into the next chamber and its fight.</summary>
        public void EnterDoor(int index)
        {
            Expect(RunPhase.Roam);
            if (index < 0 || index >= _doors.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Door = _doors[index];
            _doors = new List<Door>();
            Chamber++;
            Enemy = PlanEnemy(Chamber, Door.IsMiniboss);
            Phase = RunPhase.Fight;
        }

        /// <summary>The duel for this chamber: the player's buttons and stats against <see cref="Enemy"/>.</summary>
        public Duel StartFight()
        {
            Expect(RunPhase.Fight);
            return Duel.Start(_rules, Player, Enemy!.Loadout, PlayerStats, Enemy.Stats);
        }

        /// <summary>
        /// The fight is over. Lost: <see cref="RunPhase.Defeat"/>. Won: carry Qi over, heal, then the door's reward, or
        /// <see cref="RunPhase.Victory"/> after the boss.
        /// </summary>
        public void EndFight(Duel duel)
        {
            Expect(RunPhase.Fight);
            if (duel == null) throw new ArgumentNullException(nameof(duel));
            if (!duel.IsOver) throw new InvalidOperationException("the fight isn't over yet");
            if (duel.One.IsDown)
            {
                Hp = 0;
                Phase = RunPhase.Defeat;
                return;
            }
            Qi = Math.Min(duel.One.Qi, MaxQi);
            int heal = MaxHp * Sheet.WinHealPct / 100;
            int before = duel.One.Hp;
            Hp = Math.Min(MaxHp, before + heal);
            LastHeal = Hp - before;
            if (Chamber >= Sheet.Fights)
            {
                Phase = RunPhase.Victory;
                return;
            }
            _offers = RewardsFor(Door!);
            PicksLeft = Door!.Kind == DoorKind.AncientScroll ? Sheet.ScrollPicks : 1;
            if (_offers.Count == 0) StartRoam();
            else Phase = RunPhase.Reward;
        }

        /// <summary>
        /// Take reward <paramref name="index"/>. A new move then waits for a button; the Ancient Scroll asks again (another move);
        /// anything else goes on to roaming the cleared chamber.
        /// </summary>
        public void Choose(int index)
        {
            Expect(RunPhase.Reward);
            if (index < 0 || index >= _offers.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var o = _offers[index];
            PicksLeft--;
            switch (o.Kind)
            {
                case OfferKind.Upgrade:
                    Player.SetUpgrades(o.Move!, LevelUp(Player.UpgradesOf(o.Move!), o.Upgrade));
                    break;
                case OfferKind.Stats:
                    MaxHp += o.Hp;
                    Hp += o.Hp;
                    MaxStamina += o.Stamina;
                    MaxQi += o.Qi;
                    break;
                case OfferKind.NewMove:
                    _offers = new List<Offer>();
                    Pending = o.Move;
                    Phase = RunPhase.PlaceMove;
                    return;
            }
            // The Ancient Scroll: the next pick is a different move, still under its cap.
            _offers = PicksLeft > 0
                ? _offers.Where(x => x.Kind == OfferKind.Upgrade && x.Move != o.Move && CanUpgrade(x.Move!)).ToList()
                : new List<Offer>();
            if (_offers.Count == 0) StartRoam();
        }

        /// <summary><paramref name="u"/> with one more power-up of <paramref name="kind"/>.</summary>
        public MoveUpgrade LevelUp(MoveUpgrade u, UpgradeKind kind)
        {
            int d = u.DamageLevels + (kind == UpgradeKind.Damage ? 1 : 0);
            int s = u.StaminaLevels + (kind == UpgradeKind.Stamina ? 1 : 0);
            return new MoveUpgrade(d, s, 100 + d * Sheet.UpgradeDamagePct, Math.Max(0, 100 - s * Sheet.UpgradeStaminaPct));
        }

        /// <summary>True if <paramref name="t"/> can take another power-up: under upgrade_max, and it deals damage or costs stamina.</summary>
        public bool CanUpgrade(Technique t) =>
            Player.UpgradesOf(t).Levels < Sheet.UpgradeMax && (StringDamage(t) || StringStamina(t));

        // ------------------------------------------------------------ doors

        private void StartRoam()
        {
            PicksLeft = 0;
            _offers = new List<Offer>();
            _doors = RollDoors(Chamber + 1);
            Phase = RunPhase.Roam;
        }

        /// <summary>How many doors lead into chamber <paramref name="next"/>: 1 into the boss's, 3 into the three_doors chambers, else 2.</summary>
        public int DoorCount(int next) => next >= Sheet.Fights ? 1 : Sheet.ThreeDoors.Contains(next) ? 3 : 2;

        private List<Door> RollDoors(int next)
        {
            if (next >= Sheet.Fights) return new List<Door> { new Door(DoorKind.Boss, Rarity.Boss) };

            var doors = new List<Door>();
            if (next == 1)
            {
                // Chamber 1 always teaches a new category: one door from each list (Chan: a defensive one and the other).
                foreach (var list in new[] { Sheet.FirstDoorsA, Sheet.FirstDoorsB })
                {
                    var open = list.Where(NewCategoryOpen).ToList();
                    if (open.Count > 0) doors.Add(new Door(DoorKind.NewCategory, Rarity.Uncommon, open[_dice.Below(open.Count)]));
                }
                if (doors.Count > 0) return doors;
            }

            int count = DoorCount(next);
            // The super rare door: once a run, into a chamber from super_rare_from to super_rare_to, always by the last of them.
            if (!SuperRareShown && next >= Sheet.SuperRareFrom && next <= Sheet.SuperRareTo
                && (next == Sheet.SuperRareTo || _dice.Chance(Sheet.SuperRarePct)))
            {
                var kinds = new List<DoorKind>();
                if (StatsOpen) kinds.Add(DoorKind.Elixirs);
                if (EnhanceOpen) kinds.Add(DoorKind.AncientScroll);
                if (kinds.Count > 0)
                {
                    doors.Add(new Door(kinds[_dice.Below(kinds.Count)], Rarity.SuperRare));
                    SuperRareShown = true;
                }
            }

            for (int tries = 0; doors.Count < count && tries < 40; tries++)
            {
                var door = RollDoor(doors);
                if (door != null && !doors.Any(d => d.Kind == door.Kind)) doors.Add(door);
            }
            // Nothing else fits (everything maxed): a plain door with nothing behind it but the fight, so the run goes on.
            if (doors.Count == 0) doors.Add(new Door(DoorKind.Medicine, Rarity.Common));
            Shuffle(doors);
            return doors;
        }

        /// <summary>One door by rarity. An uncommon or rare door with nothing left to teach becomes Skill Enhance.</summary>
        private Door? RollDoor(List<Door> taken)
        {
            int roll = _dice.Below(100);
            if (roll < Sheet.RarityCommon)
            {
                var kinds = new List<DoorKind>();
                if (StatsOpen) kinds.Add(DoorKind.Medicine);
                if (EnhanceOpen) kinds.Add(DoorKind.SkillEnhance);
                return kinds.Count == 0 ? null : new Door(kinds[_dice.Below(kinds.Count)], Rarity.Common);
            }
            bool uncommon = roll < Sheet.RarityCommon + Sheet.RarityUncommon;
            var categories = Enum.GetValues(typeof(Category)).Cast<Category>()
                .Where(c => uncommon ? NewCategoryOpen(c) : SameCategoryOpen(c)).ToList();
            if (categories.Count > 0)
                return new Door(uncommon ? DoorKind.NewCategory : DoorKind.SameCategory, uncommon ? Rarity.Uncommon : Rarity.Rare,
                    categories[_dice.Below(categories.Count)]);
            return EnhanceOpen ? new Door(DoorKind.SkillEnhance, Rarity.Common) : null;
        }

        private IEnumerable<Technique> Unknown(Category c) => _pool.Where(t => t.Category == c && !Knows(t));

        /// <summary>A category you know nothing of, with a move to learn and a free button for it.</summary>
        private bool NewCategoryOpen(Category c) => HasEmptySlot && !KnowsCategory(c) && Unknown(c).Any();

        /// <summary>A category you know, with a move still to learn and a free button for it.</summary>
        private bool SameCategoryOpen(Category c) => HasEmptySlot && KnowsCategory(c) && Unknown(c).Any();

        private bool StatsOpen => MaxHp < Sheet.HpCap || MaxStamina < Sheet.StaminaCap || MaxQi < Sheet.QiCap;
        private bool EnhanceOpen => Learnt.Any(CanUpgrade);

        // ------------------------------------------------------------ rewards

        private int HpUp => Math.Min(Sheet.HpUp, Sheet.HpCap - MaxHp);
        private int StaminaUp => Math.Min(Sheet.StaminaUp, Sheet.StaminaCap - MaxStamina);
        private int QiUp => Math.Min(Sheet.QiUp, Sheet.QiCap - MaxQi);

        private List<Offer> RewardsFor(Door door)
        {
            var offers = new List<Offer>();
            switch (door.Kind)
            {
                case DoorKind.NewCategory:
                case DoorKind.SameCategory:
                {
                    if (!HasEmptySlot) break;
                    var moves = Unknown(door.Category!.Value).ToList();
                    Shuffle(moves);
                    offers.AddRange(moves.Take(Sheet.SkillChoices).Select(m => new Offer(OfferKind.NewMove, m)));
                    break;
                }
                case DoorKind.Medicine:
                    if (HpUp > 0) offers.Add(new Offer(OfferKind.Stats, hp: HpUp));
                    if (StaminaUp > 0) offers.Add(new Offer(OfferKind.Stats, stamina: StaminaUp));
                    if (QiUp > 0) offers.Add(new Offer(OfferKind.Stats, qi: QiUp));
                    break;
                case DoorKind.Elixirs:
                    if (HpUp > 0 || StaminaUp > 0) offers.Add(new Offer(OfferKind.Stats, hp: HpUp, stamina: StaminaUp));
                    if (StaminaUp > 0 || QiUp > 0) offers.Add(new Offer(OfferKind.Stats, stamina: StaminaUp, qi: QiUp));
                    if (HpUp > 0 || QiUp > 0) offers.Add(new Offer(OfferKind.Stats, hp: HpUp, qi: QiUp));
                    break;
                case DoorKind.SkillEnhance:
                case DoorKind.AncientScroll:
                    // Every move you know under its cap, each way it can go up: more damage, less stamina.
                    foreach (var s in Slots.All)
                    {
                        var t = Player[s];
                        if (t == null || !CanUpgrade(t) || offers.Any(o => o.Move == t)) continue;
                        if (StringDamage(t)) offers.Add(new Offer(OfferKind.Upgrade, t, UpgradeKind.Damage));
                        if (StringStamina(t)) offers.Add(new Offer(OfferKind.Upgrade, t, UpgradeKind.Stamina));
                    }
                    break;
            }
            return offers;
        }

        private IEnumerable<Technique> StringOf(Technique t)
        {
            for (var hit = t; hit != null; hit = _book.NextOf(hit)) yield return hit;
        }

        private bool StringDamage(Technique t) => StringOf(t).Any(h => h.Damage > 0);
        private bool StringStamina(Technique t) => StringOf(t).Any(h => h.StaminaCost > 0);

        // ------------------------------------------------------------ enemies

        /// <summary>
        /// Chamber <paramref name="n"/>'s enemy: n moves (up to enemy_moves_max), always one 外功 (enemy_first early, any of
        /// enemy_waigong from enemy_tiger_from on) on □, and the rest random moves from the other categories on random buttons.
        /// A mini boss has miniboss_hp_bonus more HP and the miniboss_brain.
        /// </summary>
        private EnemyPlan PlanEnemy(int n, bool miniboss)
        {
            bool boss = n >= Sheet.Fights;
            var loadout = new Loadout(_book);
            var waigong = n < Sheet.EnemyTigerFrom
                ? Need(Sheet.EnemyFirst)
                : Need(Sheet.EnemyWaigong[_dice.Below(Sheet.EnemyWaigong.Count)]);
            loadout.Assign(Slot.Square, waigong);

            int count = Math.Min(n, Sheet.EnemyMovesMax);
            var others = _pool.Where(t => t.Category != Category.Waigong).ToList();
            Shuffle(others);
            var free = Slots.All.Where(s => s != Slot.Square).ToList();
            Shuffle(free);
            for (int i = 0; i < count - 1 && i < others.Count && i < free.Count; i++) loadout.Assign(free[i], others[i]);

            int hp = boss ? Sheet.BossHp : Sheet.EnemyHp + (n - 1) * Sheet.EnemyHpStep + (miniboss ? Sheet.MinibossHpBonus : 0);
            var stats = new FighterStats(hp, hp, Sheet.EnemyStamina, Sheet.EnemyQi, Sheet.EnemyQiStart);
            var brain = miniboss ? Sheet.MinibossBrain : Sheet.Brains[Math.Min(n, Sheet.Brains.Count) - 1];
            int opening = miniboss ? Sheet.MinibossOpening : boss ? Sheet.BossOpening : 0;
            int stepEscape = miniboss ? Sheet.MinibossStepEscape : boss ? Sheet.BossStepEscape : 0;
            return new EnemyPlan(n, boss, miniboss, loadout, stats, brain, unchecked(Seed * 100 + n), opening, stepEscape);
        }

        // ------------------------------------------------------------ helpers

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _dice.Below(i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        private Technique Need(string id) =>
            _book.Find(id) ?? throw new KeyNotFoundException($"run.csv names '{id}', but techniques.csv has no such technique");

        private void Expect(RunPhase phase)
        {
            if (Phase != phase) throw new InvalidOperationException($"the run is at {Phase}, not {phase}");
        }
    }
}
