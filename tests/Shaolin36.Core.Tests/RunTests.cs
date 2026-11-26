using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Runs;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The demo roguelike run (Chan's chamber-and-door design), on the real sheets.</summary>
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

    /// <summary>A run past the start: 罗汉拳 (or the other scroll) on □, roaming chamber 0.</summary>
    private static Run Started(int seed = 1, int starter = 0)
    {
        var run = NewRun(seed);
        run.ChooseStarter(starter);
        Assert.True(run.Place(Slot.Square));
        return run;
    }

    /// <summary>Go through door <paramref name="door"/> and win the fight (the enemy is simply knocked out).</summary>
    private static Duel Win(Run run, int door = 0, int damageTaken = 0)
    {
        run.EnterDoor(door);
        var duel = run.StartFight();
        if (damageTaken > 0) duel.One.TakeHit(damageTaken, 0);
        duel.Two.TakeHit(duel.Two.Hp, 0);
        run.EndFight(duel);
        return duel;
    }

    /// <summary>Take rewards (the first each time) until roaming again, putting a new move on the first empty button.</summary>
    private static void TakeRewards(Run run)
    {
        while (run.Phase == RunPhase.Reward || run.Phase == RunPhase.PlaceMove)
        {
            if (run.Phase == RunPhase.Reward) run.Choose(0);
            else run.Place(Slots.All.First(s => run.Player.IsEmpty(s)));
        }
    }

    /// <summary>Clear chambers, always taking the first door, until roaming in front of the doors into <paramref name="next"/>.</summary>
    private static void ClearTo(Run run, int next, Func<IReadOnlyList<Door>, int>? pick = null)
    {
        while (run.Chamber + 1 < next)
        {
            Win(run, pick?.Invoke(run.Doors) ?? 0);
            TakeRewards(run);
        }
        Assert.Equal(RunPhase.Roam, run.Phase);
    }

    private static int DoorOf(Run run, DoorKind kind) => run.Doors.ToList().FindIndex(d => d.Kind == kind);

    /// <summary>Try seeds until the doors into some chamber include <paramref name="kind"/>; returns the run roaming in front of them.</summary>
    private static (Run run, int door) FindDoor(DoorKind kind, int fromSeed = 1)
    {
        for (int seed = fromSeed; seed < fromSeed + 400; seed++)
        {
            var run = Started(seed);
            while (run.Phase == RunPhase.Roam && run.Chamber + 1 < Sheet.Fights)
            {
                int i = DoorOf(run, kind);
                if (i >= 0) return (run, i);
                Win(run);
                TakeRewards(run);
            }
        }
        throw new InvalidOperationException($"no {kind} door came up");
    }

    // ---------------------------------------------------------------- the sheet

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
        Assert.Equal((100, 100, 80), (Sheet.PlayerHp, Sheet.PlayerStamina, Sheet.PlayerQi));
        Assert.Equal((20, 200), (Sheet.HpUp, Sheet.HpCap));
        Assert.Equal((10, 130), (Sheet.StaminaUp, Sheet.StaminaCap));
        Assert.Equal((20, 130), (Sheet.QiUp, Sheet.QiCap));
        Assert.Equal(4, Sheet.UpgradeMax);
        Assert.Equal(30, Sheet.WinHealPct);
        Assert.Equal((60, 30, 10), (Sheet.RarityCommon, Sheet.RarityUncommon, Sheet.RarityRare));
        Assert.Equal((5, 7), (Sheet.SuperRareFrom, Sheet.SuperRareTo));
        Assert.Equal(new[] { 5, 9 }, Sheet.ThreeDoors);
        Assert.Equal(new[] { Category.Qinggong, Category.Fangshen, Category.Lianqi }, Sheet.FirstDoorsA);
        Assert.Equal(new[] { Category.Neigong, Category.Qinna, Category.Dianxue }, Sheet.FirstDoorsB);
        Assert.Equal((80, 10, 200), (Sheet.EnemyHp, Sheet.EnemyHpStep, Sheet.BossHp));
        Assert.Equal((100, 100, 0), (Sheet.EnemyStamina, Sheet.EnemyQi, Sheet.EnemyQiStart));
    }

    [Fact]
    public void A_bad_sheet_is_reported()
    {
        var text = File.ReadAllText(GameDataTests.SheetPath("run.csv"))
            .Replace("starters,luohan_quan_1 huxing_quan_1", "starters,luohan_quan_1 nope")
            .Replace("brains,novice novice novice monk", "brains,novice novice monk")
            .Replace("rarity_rare,10", "rarity_rare,20")
            .Replace("first_doors_a,qinggong", "first_doors_a,flying");
        var r = RunCsv.Parse(text);
        Assert.True(r.Ok);
        var errors = r.Sheet!.Check(Book, Brains);
        Assert.Contains(errors, e => e.Contains("'nope'"));
        Assert.Contains(errors, e => e.Contains("one brain per fight"));
        Assert.Contains(errors, e => e.Contains("add up to 100"));
        Assert.Contains(errors, e => e.Contains("'flying' is not a category"));
        Assert.Contains(RunCsv.Parse("key,value,note\nfights,x,\n").Errors, e => e.Contains("whole number"));
        Assert.Contains(RunCsv.Parse("key,value,note\nfights,10,\n").Errors, e => e.Contains("missing key 'boss_hp'"));
    }

    // ---------------------------------------------------------------- the start and chamber 1

    [Fact]
    public void The_run_starts_with_two_外功_scrolls_then_an_empty_chamber_0()
    {
        var run = NewRun();
        Assert.Equal(RunPhase.ChooseStarter, run.Phase);
        Assert.Equal(new[] { "luohan_quan_1", "huxing_quan_1" }, run.Starters.Select(t => t.Id));
        run.ChooseStarter(1);
        Assert.Equal(RunPhase.PlaceMove, run.Phase);
        Assert.True(run.Place(Slot.R2));
        Assert.Equal(T("huxing_quan_1"), run.Player[Slot.R2]);
        Assert.Equal(RunPhase.Roam, run.Phase);
        Assert.Equal(0, run.Chamber);
        Assert.Null(run.Enemy);
        Assert.Equal((100, 100, 100, 80, 0), (run.MaxHp, run.Hp, run.MaxStamina, run.MaxQi, run.Qi));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Chamber_1_has_a_defensive_category_door_and_another_category_door(int seed)
    {
        var run = Started(seed);
        Assert.Equal(2, run.Doors.Count);
        Assert.All(run.Doors, d => Assert.Equal(DoorKind.NewCategory, d.Kind));
        Assert.Contains(run.Doors[0].Category!.Value, new[] { Category.Qinggong, Category.Fangshen, Category.Lianqi });
        Assert.Contains(run.Doors[1].Category!.Value, new[] { Category.Neigong, Category.Qinna, Category.Dianxue });
    }

    [Fact]
    public void All_six_categories_come_up_on_the_chamber_1_doors()
    {
        var seen = new HashSet<Category>();
        for (int seed = 1; seed <= 60; seed++) foreach (var d in Started(seed).Doors) seen.Add(d.Category!.Value);
        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void Chamber_1_is_the_first_enemy_and_its_reward_is_both_moves_of_the_door_category()
    {
        var run = Started();
        var category = run.Doors[0].Category!.Value;
        run.EnterDoor(0);
        Assert.Equal(1, run.Chamber);
        var e = run.Enemy!;
        Assert.Equal(T("luohan_quan_1"), e.Loadout[Slot.Square]);
        Assert.Equal(1, Slots.All.Count(s => !e.Loadout.IsEmpty(s)));
        Assert.Equal(80, e.Stats.MaxHp);
        Assert.Equal("novice", e.BrainId);
        Assert.False(e.IsMiniboss);
        Assert.Equal(0, e.OpeningFrames); // only mini bosses (and the boss, at boss_opening) get the opening

        var duel = run.StartFight();
        duel.Two.TakeHit(duel.Two.Hp, 0);
        run.EndFight(duel);
        Assert.Equal(RunPhase.Reward, run.Phase);
        Assert.Equal(2, run.Offers.Count);
        Assert.All(run.Offers, o => Assert.Equal(OfferKind.NewMove, o.Kind));
        Assert.All(run.Offers, o => Assert.Equal(category, o.Move!.Category));
        Assert.Equal(2, run.Offers.Select(o => o.Move).Distinct().Count());

        var move = run.Offers[1].Move!;
        run.Choose(1);
        Assert.Equal(RunPhase.PlaceMove, run.Phase);
        Assert.False(run.Place(Slot.Square)); // taken: a move is never replaced
        Assert.True(run.Place(Slot.L1));
        Assert.Equal(move, run.Player[Slot.L1]);
        Assert.Equal(RunPhase.Roam, run.Phase);
        Assert.Equal(1, run.Chamber);
    }

    // ---------------------------------------------------------------- the doors

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    public void Two_doors_three_into_chambers_5_and_9_one_into_the_boss(int seed)
    {
        var run = Started(seed);
        for (int next = 1; next <= Sheet.Fights; next++)
        {
            Assert.Equal(RunPhase.Roam, run.Phase);
            int want = next == 10 ? 1 : next == 5 || next == 9 ? 3 : 2;
            Assert.True(want == run.Doors.Count, $"into {next}: {string.Join(",", run.Doors.Select(d => d.Kind))}; learnt {run.Learnt.Count()}, hp {run.MaxHp} st {run.MaxStamina} qi {run.MaxQi}");
            // Never two of a kind (chamber 1's two doors are both new categories, but different ones).
            if (next > 1) Assert.Equal(run.Doors.Count, run.Doors.Select(d => d.Kind).Distinct().Count());
            if (next == 10) Assert.Equal(DoorKind.Boss, run.Doors[0].Kind);
            Win(run);
            if (next < Sheet.Fights) TakeRewards(run);
        }
        Assert.Equal(RunPhase.Victory, run.Phase);
    }

    [Fact]
    public void The_super_rare_door_shows_up_exactly_once_into_chamber_5_6_or_7()
    {
        var at = new HashSet<int>();
        for (int seed = 1; seed <= 60; seed++)
        {
            var run = Started(seed);
            int shown = 0;
            while (run.Chamber + 1 <= Sheet.Fights)
            {
                int next = run.Chamber + 1;
                foreach (var d in run.Doors.Where(d => d.Rarity == Rarity.SuperRare))
                {
                    shown++;
                    at.Add(next);
                    Assert.Contains(d.Kind, new[] { DoorKind.Elixirs, DoorKind.AncientScroll });
                }
                // Never take it, so it would show again if it could.
                int pick = run.Doors.ToList().FindIndex(d => d.Rarity != Rarity.SuperRare);
                Win(run, pick);
                if (run.Phase == RunPhase.Victory) break;
                TakeRewards(run);
            }
            Assert.Equal(1, shown);
        }
        Assert.Subset(new HashSet<int> { 5, 6, 7 }, at);
        Assert.Contains(5, at);
        Assert.Contains(7, at);
    }

    [Fact]
    public void Rarities_come_up_about_60_30_10()
    {
        int common = 0, uncommon = 0, rare = 0;
        for (int seed = 1; seed <= 300; seed++)
        {
            var run = Started(seed);
            Win(run);
            TakeRewards(run);
            foreach (var d in run.Doors) // the doors into chamber 2: plenty still to learn
            {
                if (d.Rarity == Rarity.Common) common++;
                else if (d.Rarity == Rarity.Uncommon) uncommon++;
                else if (d.Rarity == Rarity.Rare) rare++;
            }
        }
        int all = common + uncommon + rare;
        // Two doors of one kind are rerolled, so the split leans away from common a little; roughly right is enough.
        Assert.InRange(common * 100 / all, 40, 70);
        Assert.InRange(uncommon * 100 / all, 20, 45);
        Assert.InRange(rare * 100 / all, 4, 25);
    }

    [Fact]
    public void A_new_category_door_is_one_you_dont_know_a_rare_door_one_you_do()
    {
        for (int seed = 1; seed <= 80; seed++)
        {
            var run = Started(seed);
            for (int n = 1; n < 6; n++)
            {
                foreach (var d in run.Doors)
                {
                    if (d.Kind == DoorKind.NewCategory) Assert.False(run.KnowsCategory(d.Category!.Value));
                    if (d.Kind == DoorKind.SameCategory) Assert.True(run.KnowsCategory(d.Category!.Value));
                }
                Win(run);
                TakeRewards(run);
            }
        }
    }

    [Fact]
    public void The_rare_door_offers_the_last_move_of_a_category_you_know()
    {
        var (run, i) = FindDoor(DoorKind.SameCategory);
        var category = run.Doors[i].Category!.Value;
        Win(run, i);
        var offer = Assert.Single(run.Offers);
        Assert.Equal(OfferKind.NewMove, offer.Kind);
        Assert.Equal(category, offer.Move!.Category);
        Assert.False(run.Knows(offer.Move));
    }

    [Fact]
    public void With_all_8_buttons_full_only_medicine_skill_enhance_and_super_rare_doors_come_up()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var run = Started(seed);
            foreach (var (slot, id) in new[] { (Slot.Triangle, "tiesha_zhang"), (Slot.Circle, "fengxue"), (Slot.Cross, "qinggong_dash"),
                         (Slot.L1, "timed_parry"), (Slot.L2, "shihou_gong"), (Slot.R1, "xianliao_shu"), (Slot.R2, "qinna_shou") })
                run.Player.Assign(slot, T(id));
            for (int n = 1; n < Sheet.Fights; n++)
            {
                Win(run);
                TakeRewards(run);
                Assert.All(run.Doors, d => Assert.DoesNotContain(d.Kind, new[] { DoorKind.NewCategory, DoorKind.SameCategory }));
            }
        }
    }

    // ---------------------------------------------------------------- the rewards

    [Fact]
    public void Medicine_raises_one_of_max_hp_stamina_or_qi()
    {
        var (run, i) = FindDoor(DoorKind.Medicine);
        int hp = run.Hp, maxHp = run.MaxHp;
        Win(run, i);
        Assert.Equal(3, run.Offers.Count);
        Assert.All(run.Offers, o => Assert.Equal(OfferKind.Stats, o.Kind));
        Assert.All(run.Offers, o => Assert.Equal(1, new[] { o.Hp, o.Stamina, o.Qi }.Count(x => x > 0)));
        int hpOffer = run.Offers.ToList().FindIndex(o => o.Hp > 0);
        int healed = run.Hp;
        run.Choose(hpOffer);
        Assert.Equal(maxHp + 20, run.MaxHp);
        Assert.Equal(healed + 20, run.Hp); // HP rises with max
        Assert.Equal(RunPhase.Roam, run.Phase);
    }

    [Fact]
    public void Skill_enhance_powers_up_one_move_you_know()
    {
        var (run, i) = FindDoor(DoorKind.SkillEnhance);
        Win(run, i);
        Assert.NotEmpty(run.Offers);
        Assert.All(run.Offers, o => Assert.Equal(OfferKind.Upgrade, o.Kind));
        Assert.All(run.Offers, o => Assert.True(run.Knows(o.Move!)));
        var pick = run.Offers[0];
        int before = run.Player.UpgradesOf(pick.Move!).Levels;
        run.Choose(0);
        Assert.Equal(before + 1, run.Player.UpgradesOf(pick.Move!).Levels);
        Assert.Equal(RunPhase.Roam, run.Phase);
    }

    [Fact]
    public void The_mini_boss_has_more_hp_and_the_master_brain()
    {
        var (run, i) = FindDoor(DoorKind.Elixirs);
        int next = run.Chamber + 1;
        run.EnterDoor(i);
        Assert.True(run.Enemy!.IsMiniboss);
        Assert.Equal(80 + 10 * (next - 1) + 40, run.Enemy.Stats.MaxHp);
        Assert.Equal("master", run.Enemy.BrainId);
        // Chan: an opening after each of its attacks.
        Assert.Equal(30, run.Enemy.OpeningFrames);
        Assert.Equal(30, Sheet.MinibossOpening);
        Assert.Equal(15, Sheet.BossOpening); // the boss gets a shorter one (Chan)
    }

    [Fact]
    public void Elixirs_raise_two_of_max_hp_stamina_and_qi()
    {
        var (run, i) = FindDoor(DoorKind.Elixirs);
        Win(run, i);
        Assert.Equal(RunPhase.Reward, run.Phase);
        Assert.Equal(1, run.PicksLeft);
        Assert.All(run.Offers, o => Assert.Equal(OfferKind.Stats, o.Kind));
        Assert.Contains(run.Offers, o => o.Hp > 0 && o.Stamina > 0 && o.Qi == 0);
        Assert.Contains(run.Offers, o => o.Hp == 0 && o.Stamina > 0 && o.Qi > 0);
        Assert.Contains(run.Offers, o => o.Hp > 0 && o.Stamina == 0 && o.Qi > 0);
        int stamina = run.MaxStamina, qi = run.MaxQi;
        run.Choose(run.Offers.ToList().FindIndex(o => o.Hp == 0));
        Assert.True(run.MaxStamina > stamina && run.MaxQi > qi);
        Assert.Equal(RunPhase.Roam, run.Phase);
    }

    [Fact]
    public void The_ancient_scroll_powers_up_two_different_moves()
    {
        // Needs two moves to power up: take a category door into chamber 1 so there are two by then.
        var (run, i) = FindDoor(DoorKind.AncientScroll);
        Win(run, i);
        Assert.Equal(2, run.PicksLeft);
        var first = run.Offers[0].Move!;
        int a = run.Player.UpgradesOf(first).Levels;
        run.Choose(0);
        Assert.Equal(RunPhase.Reward, run.Phase);
        Assert.Equal(1, run.PicksLeft);
        Assert.DoesNotContain(run.Offers, o => o.Move == first);
        var second = run.Offers[0].Move!;
        int b = run.Player.UpgradesOf(second).Levels;
        run.Choose(0);
        Assert.Equal(a + 1, run.Player.UpgradesOf(first).Levels);
        Assert.Equal(b + 1, run.Player.UpgradesOf(second).Levels);
        Assert.Equal(RunPhase.Roam, run.Phase);
    }

    [Fact]
    public void A_win_heals_30_percent_of_max_hp_and_carries_qi_over()
    {
        var run = Started();
        run.EnterDoor(0);
        var duel = run.StartFight();
        duel.One.TakeHit(70, 0);
        duel.One.GainQi(33);
        duel.Two.TakeHit(duel.Two.Hp, 0);
        run.EndFight(duel);
        Assert.Equal(30 + 30, run.Hp);
        Assert.Equal(30, run.LastHeal);
        Assert.Equal(33, run.Qi);
        TakeRewards(run);

        // The next duel starts with what was carried over, stamina full.
        run.EnterDoor(0);
        var next = run.StartFight();
        Assert.Equal(run.Hp, next.One.Hp);
        Assert.Equal(33, next.One.Qi);
        Assert.Equal(run.MaxStamina, next.One.Stamina);
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
            int pick = run.Doors.ToList().FindIndex(d => !d.IsMiniboss);
            run.EnterDoor(pick);
            var e = run.Enemy!;
            Assert.Equal(n, e.Number);
            var moves = Slots.All.Where(s => !e.Loadout.IsEmpty(s)).Select(s => e.Loadout[s]!).ToList();
            Assert.Equal(System.Math.Min(n, 8), moves.Count);
            Assert.Single(moves, m => m.Category == Category.Waigong);
            if (n < 3) Assert.Equal(T("luohan_quan_1"), e.Loadout[Slot.Square]);
            Assert.Equal(n == 10 ? 200 : 80 + 10 * (n - 1), e.Stats.MaxHp);
            Assert.Equal(n == 10, e.IsBoss);
            brains.Add(e.BrainId);
            var duel = run.StartFight();
            duel.Two.TakeHit(duel.Two.Hp, 0);
            run.EndFight(duel);
            if (n < Sheet.Fights) TakeRewards(run);
        }
        Assert.Equal(RunPhase.Victory, run.Phase);
        Assert.Equal(new[] { "novice", "novice", "novice", "monk", "monk", "monk", "monk", "master", "master", "master" }, brains);
    }

    // ---------------------------------------------------------------- power-ups (unchanged)

    [Fact]
    public void A_damage_power_up_raises_every_hit_of_the_string()
    {
        var l = new Loadout(Book);
        var luohan = T("luohan_quan_1");
        l.Assign(Slot.Square, luohan);
        var once = NewRun().LevelUp(MoveUpgrade.None, UpgradeKind.Damage);
        Assert.Equal(115, once.DamagePct);
        l.SetUpgrades(luohan, once);
        Assert.Equal((12 * 115 + 50) / 100, l.UpgradedDamage(luohan, T("luohan_quan_4").Damage));
        var duel = new Duel(Rules, l, new Loadout(Book), 0, 100);
        var hits = new List<HitEvent>();
        for (int i = 0; i < 40; i++) hits.AddRange(duel.Step(i == 0 ? SlotInput.Press(Slot.Square) : SlotInput.None, SlotInput.None));
        Assert.Equal(7, Assert.Single(hits).Damage); // 6 -> 7
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
        Assert.Equal(1, l.UpgradedStaminaCost(palm, 1));
    }

    [Fact]
    public void A_maxed_move_is_never_offered_and_skill_doors_stop_when_nothing_can_go_up()
    {
        var run = Started();
        var luohan = T("luohan_quan_1");
        var u = MoveUpgrade.None;
        for (int i = 0; i < 4; i++) u = run.LevelUp(u, i % 2 == 0 ? UpgradeKind.Damage : UpgradeKind.Stamina);
        run.Player.SetUpgrades(luohan, u);
        Assert.False(run.CanUpgrade(luohan));
        // Fill the other buttons with moves that can't go up either (no damage, no stamina) or are maxed.
        foreach (var (slot, id) in new[] { (Slot.Triangle, "tiesha_zhang"), (Slot.Circle, "fengxue"), (Slot.Cross, "qinggong_dash"),
                     (Slot.L1, "timed_parry"), (Slot.L2, "shihou_gong"), (Slot.R1, "xianliao_shu"), (Slot.R2, "qinna_shou") })
        {
            run.Player.Assign(slot, T(id));
            run.Player.SetUpgrades(T(id), new MoveUpgrade(4, 0, 160, 100));
        }
        for (int n = 1; n < Sheet.Fights; n++)
        {
            Win(run);
            Assert.DoesNotContain(run.Offers, o => o.Kind == OfferKind.Upgrade);
            TakeRewards(run);
            Assert.All(run.Doors, d => Assert.DoesNotContain(d.Kind, new[] { DoorKind.SkillEnhance, DoorKind.AncientScroll }));
        }
    }

    // ---------------------------------------------------------------- losing, seeds, order

    [Fact]
    public void Losing_anywhere_restarts_the_run_from_scratch()
    {
        var run = Started();
        Win(run);
        TakeRewards(run);
        run.EnterDoor(0);
        var duel = run.StartFight();
        duel.One.TakeHit(duel.One.Hp, 0);
        run.EndFight(duel);
        Assert.Equal(RunPhase.Defeat, run.Phase);

        run.Restart();
        Assert.Equal(RunPhase.ChooseStarter, run.Phase);
        Assert.Equal(2, run.Attempt);
        Assert.Equal(0, run.Chamber);
        Assert.Empty(run.Learnt);
        Assert.False(run.SuperRareShown);
        Assert.Equal((100, 100, 100, 80, 0), (run.MaxHp, run.Hp, run.MaxStamina, run.MaxQi, run.Qi));
    }

    [Fact]
    public void The_same_seed_and_choices_give_the_same_run()
    {
        string Trace(int seed)
        {
            var run = Started(seed);
            var sb = new System.Text.StringBuilder();
            while (run.Phase == RunPhase.Roam)
            {
                sb.Append(string.Join(",", run.Doors.Select(d => $"{d.Kind}:{d.Category}"))).Append('|');
                Win(run);
                if (run.Phase == RunPhase.Victory) break;
                sb.Append(string.Join(",", run.Offers.Select(o => $"{o.Kind}:{o.Move?.Id}:{o.Upgrade}:{o.Hp}:{o.Stamina}:{o.Qi}"))).Append('\n');
                TakeRewards(run);
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
        Assert.Throws<InvalidOperationException>(() => run.EnterDoor(0));
        Assert.Throws<InvalidOperationException>(() => run.Choose(0));
        run.ChooseStarter(0);
        Assert.Throws<InvalidOperationException>(() => run.ChooseStarter(0));
    }
}
