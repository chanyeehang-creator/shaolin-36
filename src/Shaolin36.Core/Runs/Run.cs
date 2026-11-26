using System;
using System.Collections.Generic;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Runs
{
    /// <summary>Where a run is: picking the first move, putting a move on a button, fighting, picking a reward, or over.</summary>
    public enum RunPhase
    {
        /// <summary>At the start: choose one of the starters (罗汉拳 or 虎形拳).</summary>
        ChooseStarter,
        /// <summary>A move was just learnt: choose an empty button for it.</summary>
        PlaceMove,
        /// <summary>Ready for, or in, fight <see cref="Run.FightNumber"/>.</summary>
        Fight,
        /// <summary>Won a fight: choose one of <see cref="Run.Offers"/>.</summary>
        Reward,
        /// <summary>Beat the boss: the run is won.</summary>
        Victory,
        /// <summary>Lost a fight: the run is over and starts again from the beginning.</summary>
        Defeat,
    }

    /// <summary>The kinds of reward after a win.</summary>
    public enum OfferKind
    {
        /// <summary>Power up one move the player knows (<see cref="Offer.Upgrade"/> says how).</summary>
        Upgrade,
        MaxHp,
        MaxStamina,
        MaxQi,
        /// <summary>Learn a new move (it then goes on an empty button).</summary>
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
        public Offer(OfferKind kind, Technique? move = null, UpgradeKind upgrade = UpgradeKind.Damage, int amount = 0)
        {
            Kind = kind;
            Move = move;
            Upgrade = upgrade;
            Amount = amount;
        }

        public OfferKind Kind { get; }

        /// <summary>The move to power up (<see cref="OfferKind.Upgrade"/>) or to learn (<see cref="OfferKind.NewMove"/>).</summary>
        public Technique? Move { get; }

        /// <summary>For <see cref="OfferKind.Upgrade"/>: which power-up.</summary>
        public UpgradeKind Upgrade { get; }

        /// <summary>For the max HP, stamina and Qi rewards: how much the max goes up.</summary>
        public int Amount { get; }
    }

    /// <summary>Who the next fight is against: its moves on its buttons, its stats and the brain that plays it.</summary>
    public sealed class EnemyPlan
    {
        public EnemyPlan(int number, bool isBoss, Loadout loadout, FighterStats stats, string brainId, int brainSeed)
        {
            BrainSeed = brainSeed;
            Number = number;
            IsBoss = isBoss;
            Loadout = loadout;
            Stats = stats;
            BrainId = brainId;
        }

        /// <summary>Which fight, from 1.</summary>
        public int Number { get; }
        public bool IsBoss { get; }
        public Loadout Loadout { get; }
        public FighterStats Stats { get; }
        /// <summary>A row id of brains.csv.</summary>
        public string BrainId { get; }
        /// <summary>The seed for its brain: fixed by the run's seed and the fight number.</summary>
        public int BrainSeed { get; }
    }

    /// <summary>
    /// The demo roguelike run (Chan's test run). Pick 罗汉拳 or 虎形拳 and a button for it, then fight enemy after enemy. Each win
    /// heals win_heal_pct of max HP and offers rewards (a move power-up, more max HP, stamina or Qi, or a new move on an empty
    /// button); Qi carries over from fight to fight. Each enemy has more HP and one more move than the last, and a smarter brain
    /// later on; the last is the boss. Lose once and the run is over: <see cref="Restart"/> starts it again.
    ///
    /// Every random choice is rolled on a <see cref="Dice"/> from the run's seed, so the same seed and choices give the same run.
    /// Nothing here knows about the screen: the game asks for <see cref="StartFight"/>'s duel, plays it, and hands it back to
    /// <see cref="EndFight"/>.
    /// </summary>
    public sealed class Run
    {
        private readonly Rules _rules;
        private readonly TechniqueBook _book;
        private readonly List<Technique> _pool;
        private readonly List<Technique> _starters;
        private Dice _dice;
        private List<Offer> _offers = new List<Offer>();

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

        /// <summary>The fight being played or coming up, from 1 to <see cref="RunSheet.Fights"/>.</summary>
        public int FightNumber { get; private set; }

        /// <summary>The player's buttons. A run never replaces a move: new ones go on empty buttons.</summary>
        public Loadout Player { get; private set; }

        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public int MaxStamina { get; private set; }
        public int MaxQi { get; private set; }
        public int Qi { get; private set; }

        /// <summary>HP healed after the last win (for the screen).</summary>
        public int LastHeal { get; private set; }

        /// <summary>The 外功 moves to choose from at the start.</summary>
        public IReadOnlyList<Technique> Starters => _starters;

        /// <summary>The move waiting for a button (<see cref="RunPhase.PlaceMove"/>).</summary>
        public Technique? Pending { get; private set; }

        /// <summary>The rewards on offer (<see cref="RunPhase.Reward"/>).</summary>
        public IReadOnlyList<Offer> Offers => _offers;

        /// <summary>The next or current enemy (<see cref="RunPhase.Fight"/>).</summary>
        public EnemyPlan? Enemy { get; private set; }

        /// <summary>The player's stats for the next fight: HP and Qi carried over, stamina full.</summary>
        public FighterStats PlayerStats => new FighterStats(MaxHp, Hp, MaxStamina, MaxQi, Qi);

        /// <summary>The moves the player has on a button.</summary>
        public IEnumerable<Technique> Learnt => Slots.All.Select(s => Player[s]).Where(t => t != null).Select(t => t!).Distinct();

        /// <summary>True if the player knows <paramref name="t"/>.</summary>
        public bool Knows(Technique t) => Learnt.Contains(t);

        public bool HasEmptySlot => Slots.All.Any(s => Player.IsEmpty(s));

        /// <summary>Start again from the beginning: base stats, no moves, fight 1. A new seed, counted on from the first.</summary>
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
            FightNumber = 1;
            LastHeal = 0;
            Pending = null;
            Enemy = null;
            _offers = new List<Offer>();
            Phase = RunPhase.ChooseStarter;
        }

        /// <summary>Pick starter <paramref name="index"/>; next, choose its button.</summary>
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
            ReadyFight();
            return true;
        }

        /// <summary>The duel for the coming fight: the player's buttons and stats against <see cref="Enemy"/>.</summary>
        public Duel StartFight()
        {
            Expect(RunPhase.Fight);
            return Duel.Start(_rules, Player, Enemy!.Loadout, PlayerStats, Enemy.Stats);
        }

        /// <summary>
        /// The fight is over. Lost: <see cref="RunPhase.Defeat"/>. Won: carry Qi over, heal, then the next fight's rewards, or
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
            if (FightNumber >= Sheet.Fights)
            {
                Phase = RunPhase.Victory;
                return;
            }
            FightNumber++;
            _offers = RollOffers();
            Phase = _offers.Count > 0 ? RunPhase.Reward : RunPhase.Fight;
            if (Phase == RunPhase.Fight) ReadyFight();
        }

        /// <summary>Take reward <paramref name="index"/>. A new move then waits for a button; anything else goes to the next fight.</summary>
        public void Choose(int index)
        {
            Expect(RunPhase.Reward);
            if (index < 0 || index >= _offers.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var o = _offers[index];
            _offers = new List<Offer>();
            switch (o.Kind)
            {
                case OfferKind.Upgrade:
                    Player.SetUpgrades(o.Move!, LevelUp(Player.UpgradesOf(o.Move!), o.Upgrade));
                    break;
                case OfferKind.MaxHp:
                    MaxHp += o.Amount;
                    Hp += o.Amount;
                    break;
                case OfferKind.MaxStamina:
                    MaxStamina += o.Amount;
                    break;
                case OfferKind.MaxQi:
                    MaxQi += o.Amount;
                    break;
                case OfferKind.NewMove:
                    Pending = o.Move;
                    Phase = RunPhase.PlaceMove;
                    return;
            }
            ReadyFight();
        }

        /// <summary><paramref name="u"/> with one more power-up of <paramref name="kind"/>.</summary>
        public MoveUpgrade LevelUp(MoveUpgrade u, UpgradeKind kind)
        {
            int d = u.DamageLevels + (kind == UpgradeKind.Damage ? 1 : 0);
            int s = u.StaminaLevels + (kind == UpgradeKind.Stamina ? 1 : 0);
            return new MoveUpgrade(d, s, 100 + d * Sheet.UpgradeDamagePct, Math.Max(0, 100 - s * Sheet.UpgradeStaminaPct));
        }

        // ------------------------------------------------------------ rewards

        private List<Offer> RollOffers()
        {
            var kinds = new List<OfferKind>();
            if (Learnt.Any(CanUpgrade)) kinds.Add(OfferKind.Upgrade);
            if (MaxHp < Sheet.HpCap && Sheet.HpUp > 0) kinds.Add(OfferKind.MaxHp);
            if (MaxStamina < Sheet.StaminaCap && Sheet.StaminaUp > 0) kinds.Add(OfferKind.MaxStamina);
            if (MaxQi < Sheet.QiCap && Sheet.QiUp > 0) kinds.Add(OfferKind.MaxQi);
            if (HasEmptySlot && _pool.Any(t => !Knows(t))) kinds.Add(OfferKind.NewMove);

            Shuffle(kinds);
            var offers = new List<Offer>();
            foreach (var kind in kinds.Take(Sheet.Offers))
            {
                switch (kind)
                {
                    case OfferKind.Upgrade:
                    {
                        var moves = Learnt.Where(CanUpgrade).ToList();
                        var move = moves[_dice.Below(moves.Count)];
                        var ways = new List<UpgradeKind>();
                        if (StringDamage(move)) ways.Add(UpgradeKind.Damage);
                        if (StringStamina(move)) ways.Add(UpgradeKind.Stamina);
                        offers.Add(new Offer(kind, move, ways[_dice.Below(ways.Count)]));
                        break;
                    }
                    case OfferKind.MaxHp:
                        offers.Add(new Offer(kind, amount: Math.Min(Sheet.HpUp, Sheet.HpCap - MaxHp)));
                        break;
                    case OfferKind.MaxStamina:
                        offers.Add(new Offer(kind, amount: Math.Min(Sheet.StaminaUp, Sheet.StaminaCap - MaxStamina)));
                        break;
                    case OfferKind.MaxQi:
                        offers.Add(new Offer(kind, amount: Math.Min(Sheet.QiUp, Sheet.QiCap - MaxQi)));
                        break;
                    case OfferKind.NewMove:
                    {
                        // A category first (any of the 7 with a move still to learn), then a move from it.
                        var unknown = _pool.Where(t => !Knows(t)).ToList();
                        var categories = unknown.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();
                        var category = categories[_dice.Below(categories.Count)];
                        var moves = unknown.Where(t => t.Category == category).ToList();
                        offers.Add(new Offer(kind, moves[_dice.Below(moves.Count)]));
                        break;
                    }
                }
            }
            return offers;
        }

        /// <summary>True if <paramref name="t"/> can take another power-up: under upgrade_max, and it deals damage or costs stamina.</summary>
        public bool CanUpgrade(Technique t) =>
            Player.UpgradesOf(t).Levels < Sheet.UpgradeMax && (StringDamage(t) || StringStamina(t));

        private IEnumerable<Technique> StringOf(Technique t)
        {
            for (var hit = t; hit != null; hit = _book.NextOf(hit)) yield return hit;
        }

        private bool StringDamage(Technique t) => StringOf(t).Any(h => h.Damage > 0);
        private bool StringStamina(Technique t) => StringOf(t).Any(h => h.StaminaCost > 0);

        // ------------------------------------------------------------ enemies

        private void ReadyFight()
        {
            Phase = RunPhase.Fight;
            Enemy = PlanEnemy(FightNumber);
        }

        /// <summary>
        /// Enemy <paramref name="n"/>: n moves (up to enemy_moves_max), always one 外功 (enemy_first early, any of enemy_waigong from
        /// enemy_tiger_from on) on □, and the rest random moves from the other categories on random buttons.
        /// </summary>
        private EnemyPlan PlanEnemy(int n)
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

            int hp = boss ? Sheet.BossHp : Sheet.EnemyHp + (n - 1) * Sheet.EnemyHpStep;
            var stats = new FighterStats(hp, hp, Sheet.EnemyStamina, Sheet.EnemyQi, Sheet.EnemyQiStart);
            var brain = Sheet.Brains[Math.Min(n, Sheet.Brains.Count) - 1];
            return new EnemyPlan(n, boss, loadout, stats, brain, unchecked(Seed * 100 + n));
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
