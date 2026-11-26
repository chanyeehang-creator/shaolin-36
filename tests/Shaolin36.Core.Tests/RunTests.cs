using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Runs;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The demo roguelike run (Chan's test run), on the real sheets.</summary>
public class RunTests
{
    private static readonly Rules Rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
    private static readonly IReadOnlyList<Technique> All = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
    private static readonly TechniqueBook Book = new(All);
    private static readonly BrainSheet Brains = BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv"))).Sheet!;
    private static readonly RunSheet Sheet = RunCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("run.csv"))).Sheet!;

    /// <summary>Moves that go on a button: everything that isn't a later hit of a string.</summary>
    private static readonly List<Technique> Learnable = All.Where(t => !All.Any(o => o.Next == t.Id)).ToList();

    private static Technique T(string id) => Book.Find(id)!;

    private static Run NewRun(int seed = 1) => new(Sheet, Rules, Book, Learnable, seed);

    /// <summary>A run past the start: 罗汉拳 (or the other starter) on □, at fight 1.</summary>
    private static Run Started(int seed = 1, int starter = 0)
    {
        var run = NewRun(seed);
        run.ChooseStarter(starter);
        Assert.True(run.Place(Slot.Square));
        return run;
    }

    /// <summary>Play the coming fight to a win (the enemy is simply knocked out) and hand it back.</summary>
    private static Duel Win(Run run, int damageTaken = 0)
    {
        var duel = run.StartFight();
        if (damageTaken > 0) duel.One.TakeHit(damageTaken, 0);
        duel.Two.TakeHit(duel.Two.Hp, 0);
        run.EndFight(duel);
        return duel;
    }

    /// <summary>Take the first offer of <paramref name="kind"/>, or the first offer, then place any new move on the first empty button.</summary>
    private static void TakeReward(Run run, OfferKind? kind = null)
    {
        int i = kind == null ? 0 : run.Offers.ToList().FindIndex(o => o.Kind == kind);
        if (i < 0) i = 0;
        run.Choose(i);
        if (run.Phase == RunPhase.PlaceMove) run.Place(Slots.All.First(s => run.Player.IsEmpty(s)));
    }

    [Fact]
    public void Run_sheet_passes_the_check()
    {
        var r = RunCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("run.csv")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Empty(r.Sheet!.Check(Book, Brains));
    }

    [Fact]
    public void Chans_numbers_are_on_the_sheet()
    {
        Assert.Equal(10, Sheet.Fights);
        Assert.Equal(100, Sheet.PlayerHp);
        Assert.Equal(80, Sheet.PlayerStamina);
        Assert.Equal(80, Sheet.PlayerQi);
        Assert.Equal((20, 200), (Sheet.HpUp, Sheet.HpCap));
        Assert.Equal((10, 130), (Sheet.StaminaUp, Sheet.StaminaCap));
        Assert.Equal((20, 130), (Sheet.QiUp, Sheet.QiCap));
        Assert.Equal(4, Sheet.UpgradeMax);
        Assert.Equal(50, Sheet.WinHealPct);
        Assert.Equal(3, Sheet.Offers);
        Assert.Equal((80, 10, 200), (Sheet.EnemyHp, Sheet.EnemyHpStep, Sheet.BossHp));
        Assert.Equal((100, 100, 0), (Sheet.EnemyStamina, Sheet.EnemyQi, Sheet.EnemyQiStart));
    }

    [Fact]
    public void A_bad_sheet_is_reported()
    {
        var text = File.ReadAllText(GameDataTests.SheetPath("run.csv"))
            .Replace("starters,luohan_quan_1 huxing_quan_1", "starters,luohan_quan_1 nope")
            .Replace("brains,novice novice novice monk", "brains,novice novice monk");
        var r = RunCsv.Parse(text);
        Assert.True(r.Ok);
        var errors = r.Sheet!.Check(Book, Brains);
        Assert.Contains(errors, e => e.Contains("'nope'"));
        Assert.Contains(errors, e => e.Contains("one brain per fight"));
        Assert.Contains(RunCsv.Parse("key,value,note\nfights,x,\n").Errors, e => e.Contains("whole number"));
        Assert.Contains(RunCsv.Parse("key,value,note\nfights,10,\n").Errors, e => e.Contains("missing key 'boss_hp'"));
    }

    [Fact]
    public void The_run_starts_with_a_choice_of_luohan_or_huxing_and_any_button_for_it()
    {
        var run = NewRun();
        Assert.Equal(RunPhase.ChooseStarter, run.Phase);
        Assert.Equal(new[] { "luohan_quan_1", "huxing_quan_1" }, run.Starters.Select(t => t.Id));
        run.ChooseStarter(1);
        Assert.Equal(RunPhase.PlaceMove, run.Phase);
        Assert.Equal("huxing_quan_1", run.Pending!.Id);
        Assert.True(run.Place(Slot.R2));
        Assert.Equal(RunPhase.Fight, run.Phase);
        Assert.Equal(T("huxing_quan_1"), run.Player[Slot.R2]);
        Assert.Single(run.Learnt);
        Assert.Equal((100, 100, 80, 80, 0), (run.MaxHp, run.Hp, run.MaxStamina, run.MaxQi, run.Qi));
    }

    [Fact]
    public void The_first_enemy_only_knows_luohan_quan_has_80_hp_and_the_easiest_brain()
    {
        var run = Started();
        var e = run.Enemy!;
        Assert.Equal(1, e.Number);
        Assert.False(e.IsBoss);
        Assert.Equal(T("luohan_quan_1"), e.Loadout[Slot.Square]);
        Assert.Equal(1, Slots.All.Count(s => !e.Loadout.IsEmpty(s)));
        Assert.Equal(80, e.Stats.MaxHp);
        Assert.Equal((100, 100, 0), (e.Stats.MaxStamina, e.Stats.MaxQi, e.Stats.Qi));
        Assert.Equal("novice", e.BrainId);

        var duel = run.StartFight();
        Assert.Equal((100, 80, 80), (duel.One.MaxHp, duel.One.MaxStamina, duel.One.MaxQi));
        Assert.Equal((80, 100, 100, 0), (duel.Two.MaxHp, duel.Two.MaxStamina, duel.Two.MaxQi, duel.Two.Qi));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void Each_enemy_has_one_more_move_10_more_hp_and_the_boss_has_200(int seed)
    {
        var run = Started(seed);
        var brains = new List<string>();
        for (int n = 1; n <= Sheet.Fights; n++)
        {
            Assert.Equal(RunPhase.Fight, run.Phase);
            var e = run.Enemy!;
            Assert.Equal(n, e.Number);
            var moves = Slots.All.Where(s => !e.Loadout.IsEmpty(s)).Select(s => e.Loadout[s]!).ToList();
            Assert.Equal(System.Math.Min(n, 8), moves.Count);
            Assert.Equal(moves.Count, moves.Distinct().Count());
            // Exactly one 外功, on □: 罗汉拳 for the first two, then 罗汉拳 or 虎形拳.
            Assert.Single(moves, m => m.Category == Category.Waigong);
            Assert.Equal(Category.Waigong, e.Loadout[Slot.Square]!.Category);
            if (n < 3) Assert.Equal(T("luohan_quan_1"), e.Loadout[Slot.Square]);
            Assert.Equal(n == 10 ? 200 : 80 + 10 * (n - 1), e.Stats.MaxHp);
            Assert.Equal(n == 10, e.IsBoss);
            brains.Add(e.BrainId);
            Win(run);
            if (n < Sheet.Fights) TakeReward(run);
        }
        Assert.Equal(RunPhase.Victory, run.Phase);
        Assert.Equal(new[] { "novice", "novice", "novice", "monk", "monk", "monk", "monk", "master", "master", "master" }, brains);
    }

    [Fact]
    public void Later_enemies_sometimes_use_huxing_quan()
    {
        bool tiger = false;
        for (int seed = 1; seed <= 20 && !tiger; seed++)
        {
            var run = Started(seed);
            for (int n = 1; n < 6; n++)
            {
                if (run.Enemy!.Loadout[Slot.Square] == T("huxing_quan_1")) tiger = true;
                Win(run);
                TakeReward(run);
            }
        }
        Assert.True(tiger);
    }

    [Fact]
    public void A_win_heals_half_of_max_hp_and_carries_qi_over()
    {
        var run = Started();
        var duel = run.StartFight();
        duel.One.TakeHit(70, 0);
        duel.One.GainQi(33);
        duel.Two.TakeHit(duel.Two.Hp, 0);
        run.EndFight(duel);
        Assert.Equal(30 + 50, run.Hp);
        Assert.Equal(50, run.LastHeal);
        Assert.Equal(33, run.Qi);
        Assert.Equal(RunPhase.Reward, run.Phase);
        Assert.Equal(2, run.FightNumber);

        // Never above max.
        run.Choose(run.Offers.ToList().FindIndex(o => o.Kind != OfferKind.NewMove && o.Kind != OfferKind.MaxHp));
        Win(run, damageTaken: 10);
        Assert.Equal(100, run.Hp);

        // The next duel starts with what was carried over.
        TakeReward(run, OfferKind.MaxStamina);
        var next = run.StartFight();
        Assert.Equal(run.Hp, next.One.Hp);
        Assert.Equal(run.Qi, next.One.Qi);
        Assert.Equal(run.MaxStamina, next.One.Stamina);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void Each_win_offers_three_rewards_of_different_kinds_never_a_move_you_know(int seed)
    {
        var run = Started(seed);
        for (int n = 1; n < Sheet.Fights; n++)
        {
            Win(run);
            Assert.Equal(RunPhase.Reward, run.Phase);
            Assert.Equal(3, run.Offers.Count);
            Assert.Equal(3, run.Offers.Select(o => o.Kind).Distinct().Count());
            foreach (var o in run.Offers)
            {
                if (o.Kind == OfferKind.NewMove) Assert.False(run.Knows(o.Move!));
                if (o.Kind == OfferKind.Upgrade) Assert.True(run.Knows(o.Move!));
            }
            TakeReward(run, OfferKind.NewMove);
        }
    }

    [Fact]
    public void A_new_move_goes_on_an_empty_button_and_never_replaces_one()
    {
        var run = Started();
        run.Choose(ForceOffer(run, OfferKind.NewMove));
        Assert.Equal(RunPhase.PlaceMove, run.Phase);
        var move = run.Pending!;
        Assert.False(run.Place(Slot.Square)); // 罗汉拳 is there
        Assert.Equal(RunPhase.PlaceMove, run.Phase);
        Assert.Equal(T("luohan_quan_1"), run.Player[Slot.Square]);
        var empty = Slots.All.First(s => run.Player.IsEmpty(s));
        Assert.True(run.Place(empty));
        Assert.Equal(move, run.Player[empty]);
        Assert.Equal(RunPhase.Fight, run.Phase);
    }

    [Fact]
    public void With_all_8_buttons_full_no_new_move_is_offered()
    {
        var run = Started();
        foreach (var (slot, id) in new[] { (Slot.Triangle, "tiesha_zhang"), (Slot.Circle, "fengxue"), (Slot.Cross, "qinggong_dash"),
                     (Slot.L1, "timed_parry"), (Slot.L2, "shihou_gong"), (Slot.R1, "xianliao_shu"), (Slot.R2, "qinna_shou") })
            run.Player.Assign(slot, T(id));
        Assert.False(run.HasEmptySlot);
        for (int n = 1; n < Sheet.Fights; n++)
        {
            Win(run);
            Assert.DoesNotContain(run.Offers, o => o.Kind == OfferKind.NewMove);
            TakeReward(run);
        }
    }

    [Fact]
    public void Max_hp_stamina_and_qi_stop_at_their_caps()
    {
        var run = Started();
        for (int n = 1; n < Sheet.Fights; n++)
        {
            Win(run);
            var hp = run.Offers.FirstOrDefault(o => o.Kind == OfferKind.MaxHp);
            if (hp != null) Assert.Equal(20, hp.Amount);
            var qi = run.Offers.FirstOrDefault(o => o.Kind == OfferKind.MaxQi);
            if (qi != null) Assert.Equal(System.Math.Min(20, 130 - run.MaxQi), qi.Amount);
            // Alternate so all three climb.
            var want = (n % 3) switch { 0 => OfferKind.MaxHp, 1 => OfferKind.MaxQi, _ => OfferKind.MaxStamina };
            TakeReward(run, want);
            Assert.True(run.MaxHp <= 200 && run.MaxStamina <= 130 && run.MaxQi <= 130);
        }
        // Qi climbs 80 -> 100 -> 120 -> 130 and then is never offered again.
        var full = Started();
        for (int i = 0; i < 3; i++) full.Choose(ForceOffer(full, OfferKind.MaxQi));
        Assert.Equal(130, full.MaxQi);
    }

    /// <summary>Win until an offer of <paramref name="kind"/> comes up and return its index.</summary>
    private static int ForceOffer(Run run, OfferKind kind)
    {
        for (int guard = 0; guard < Sheet.Fights; guard++)
        {
            if (run.Phase == RunPhase.Fight) Win(run);
            int i = run.Offers.ToList().FindIndex(o => o.Kind == kind);
            if (i >= 0) return i;
            TakeReward(run, OfferKind.MaxStamina);
        }
        throw new InvalidOperationException($"no {kind} offer came up");
    }

    [Fact]
    public void Max_hp_reward_raises_hp_with_it()
    {
        var run = Started();
        int i = ForceOffer(run, OfferKind.MaxHp);
        int hp = run.Hp, max = run.MaxHp;
        run.Choose(i);
        Assert.Equal(max + 20, run.MaxHp);
        Assert.Equal(hp + 20, run.Hp);
    }

    [Fact]
    public void A_damage_power_up_raises_every_hit_of_the_string()
    {
        var l = new Loadout(Book);
        var luohan = T("luohan_quan_1");
        l.Assign(Slot.Square, luohan);
        var run = NewRun();
        var once = run.LevelUp(MoveUpgrade.None, UpgradeKind.Damage);
        Assert.Equal(115, once.DamagePct);
        l.SetUpgrades(luohan, once);
        Assert.Equal((6 * 115 + 50) / 100, l.UpgradedDamage(luohan, 6));
        Assert.Equal((12 * 115 + 50) / 100, l.UpgradedDamage(luohan, T("luohan_quan_4").Damage));

        // In a duel: the first 罗汉拳 hit lands 7 instead of 6.
        var other = new Loadout(Book);
        var duel = new Duel(Rules, l, other, 0, 100);
        var hits = new List<HitEvent>();
        for (int i = 0; i < 40; i++) hits.AddRange(duel.Step(i == 0 ? SlotInput.Press(Slot.Square) : SlotInput.None, SlotInput.None));
        var hit = Assert.Single(hits);
        Assert.Equal(7, hit.Damage);
    }

    [Fact]
    public void A_stamina_power_up_makes_the_move_cheaper()
    {
        var l = new Loadout(Book);
        var palm = T("tiesha_zhang");
        l.Assign(Slot.Triangle, palm);
        var run = NewRun();
        var u = run.LevelUp(run.LevelUp(MoveUpgrade.None, UpgradeKind.Stamina), UpgradeKind.Stamina);
        Assert.Equal(70, u.StaminaPct);
        l.SetUpgrades(palm, u);
        var duel = new Duel(Rules, l, new Loadout(Book), 0, 500);
        duel.Step(SlotInput.Press(Slot.Triangle), SlotInput.None);
        Assert.Equal(Rules.StaminaMax - (palm.StaminaCost * 70 + 50) / 100, duel.One.Stamina);
        // Never below 1 when it cost something.
        Assert.Equal(1, l.UpgradedStaminaCost(palm, 1));
    }

    [Fact]
    public void A_move_takes_at_most_4_power_ups()
    {
        var run = Started();
        var luohan = T("luohan_quan_1");
        Assert.True(run.CanUpgrade(luohan));
        var u = MoveUpgrade.None;
        for (int i = 0; i < 4; i++) u = run.LevelUp(u, i % 2 == 0 ? UpgradeKind.Damage : UpgradeKind.Stamina);
        run.Player.SetUpgrades(luohan, u);
        Assert.Equal(4, run.Player.UpgradesOf(luohan).Levels);
        Assert.False(run.CanUpgrade(luohan));
        // 罗汉拳 is the only move: no power-up offers any more.
        Win(run);
        Assert.DoesNotContain(run.Offers, o => o.Kind == OfferKind.Upgrade);
    }

    [Fact]
    public void The_upgrade_reward_picks_a_learnt_move_and_levels_it()
    {
        var run = Started();
        int i = ForceOffer(run, OfferKind.Upgrade);
        var o = run.Offers[i];
        var before = run.Player.UpgradesOf(o.Move!).Levels;
        run.Choose(i);
        Assert.Equal(before + 1, run.Player.UpgradesOf(o.Move!).Levels);
        Assert.Equal(RunPhase.Fight, run.Phase);
    }

    [Fact]
    public void Losing_ends_the_run_and_restart_starts_over()
    {
        var run = Started();
        Win(run);
        TakeReward(run, OfferKind.NewMove);
        var duel = run.StartFight();
        duel.One.TakeHit(duel.One.Hp, 0);
        run.EndFight(duel);
        Assert.Equal(RunPhase.Defeat, run.Phase);

        run.Restart();
        Assert.Equal(RunPhase.ChooseStarter, run.Phase);
        Assert.Equal(2, run.Attempt);
        Assert.Equal(1, run.FightNumber);
        Assert.Empty(run.Learnt);
        Assert.Equal((100, 100, 80, 80, 0), (run.MaxHp, run.Hp, run.MaxStamina, run.MaxQi, run.Qi));
    }

    [Fact]
    public void The_same_seed_and_choices_give_the_same_run()
    {
        string Trace(int seed)
        {
            var run = Started(seed);
            var sb = new System.Text.StringBuilder();
            for (int n = 1; n < Sheet.Fights; n++)
            {
                sb.Append(string.Join(",", Slots.All.Select(s => run.Enemy!.Loadout[s]?.Id ?? "-"))).Append('|');
                Win(run);
                sb.Append(string.Join(",", run.Offers.Select(o => $"{o.Kind}:{o.Move?.Id}:{o.Upgrade}:{o.Amount}"))).Append('\n');
                TakeReward(run);
            }
            return sb.ToString();
        }
        Assert.Equal(Trace(5), Trace(5));
        Assert.NotEqual(Trace(5), Trace(6));
    }

    [Fact]
    public void Steps_out_of_order_throw()
    {
        var run = NewRun();
        Assert.Throws<InvalidOperationException>(() => run.StartFight());
        Assert.Throws<InvalidOperationException>(() => run.Choose(0));
        run.ChooseStarter(0);
        Assert.Throws<InvalidOperationException>(() => run.ChooseStarter(0));
    }
}
