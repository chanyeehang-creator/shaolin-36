using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Stage 2 step 2: knockback. A landed hit pushes the defender straight back by the technique's
/// knockback, slid over knockback_frames ticks; a same-weight clash pushes both apart.
/// </summary>
public class KnockbackTests
{
    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    // Lands on frame 5, pushes 60 cm.
    private static readonly Technique Shove = WithKnockback(Make.Move("shove", startup: 5, active: 3, recovery: 12, window: 0,
        perfect: 0, damage: 6, hitstun: 30, stamina: 0, reach: 200), 60);
    private static readonly Technique Parry = Make.ParryMove(stamina: 0); // window frames 3-12, perfect 3-6

    /// <summary><paramref name="t"/> with <paramref name="cm"/> knockback; with empowerQi, an armored empowered version.</summary>
    private static Technique WithKnockback(Technique t, int cm, int empowerQi = 0) =>
        new(t.Id, t.Name, t.Category, t.Weight, t.IsWeapon, t.StartupFrames, t.ActiveFrames, t.RecoveryFrames,
            t.ClashWindowFrames, t.PerfectClashFrames, t.StaminaCost, t.QiCost, t.Damage, t.Clashable, t.HitstunFrames,
            t.ReachCm, t.Next, t.ChainWindowFrames, t.PerfectParryFrames, empowerQi, 100, empowerQi > 0, cm);

    private static Rules RulesWith(int knockbackFrames = 8, int recoilPush = 25, int arena = 1400)
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("qi_max", "100"), ("qi_start", "0"), ("knockback_frames", knockbackFrames.ToString()),
            ("clash_recoil_push", recoilPush.ToString()), ("arena_width", arena.ToString()), ("start_distance", "100"),
            ("min_distance", "80")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique square, Technique? l1 = null)
    {
        var book = new TechniqueBook(new[] { square, l1 ?? Parry }.Distinct());
        var l = new Loadout(book);
        l.Assign(Slot.Square, square);
        if (l1 != null) l.Assign(Slot.L1, l1);
        return l;
    }

    [Fact]
    public void A_landed_hit_slides_the_defender_back_over_knockback_frames()
    {
        var d = new Duel(RulesWith(), Holding(Shove), Holding(Shove), 0, 100);
        for (int t = 0; t <= 5; t++) d.Step(t == 0 ? Square : None, None);
        var hit = Assert.Single(d.Hits);
        Assert.Equal(60, hit.KnockbackCm);
        Assert.Equal(100, d.Two.PositionCm); // nothing moves on the hit tick itself

        var steps = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            int before = d.Two.PositionCm;
            d.Step(None, None);
            steps.Add(d.Two.PositionCm - before);
        }
        // 60 cm over 8 ticks, whole centimetres, larger steps first; then it stops.
        Assert.Equal(new[] { 8, 8, 8, 8, 7, 7, 7, 7, 0, 0 }, steps);
        Assert.Equal(160, d.Two.PositionCm);
        Assert.Equal(0, d.One.PositionCm);
    }

    [Fact]
    public void Knockback_pushes_away_from_the_attacker_whichever_side_they_are_on()
    {
        var d = new Duel(RulesWith(), Holding(Shove), Holding(Shove), 0, 100);
        for (int t = 0; t <= 20; t++) d.Step(None, t == 0 ? Square : None);
        Assert.Equal(-60, d.One.PositionCm);
        Assert.Equal(100, d.Two.PositionCm);
    }

    [Fact]
    public void The_arena_end_stops_the_push()
    {
        // Arena 400 wide: the ends are at -200 and 200. Two stands at 170 and can only go 30 cm.
        var d = new Duel(RulesWith(arena: 400), Holding(Shove), Holding(Shove), 70, 170);
        for (int t = 0; t <= 20; t++) d.Step(t == 0 ? Square : None, None);
        Assert.Equal(200, d.Two.PositionCm);
    }

    [Fact]
    public void A_second_push_adds_to_what_is_left_and_restarts_the_slide()
    {
        // knockback_frames 40: 60 cm in 40 ticks (2 cm a tick, then 1), so the first push is still sliding when One
        // shoves again (pressed on tick 20 once idle, landing on tick 25).
        var d = new Duel(RulesWith(knockbackFrames: 40), Holding(Shove), Holding(Shove), 0, 100);
        for (int t = 0; t <= 25; t++) d.Step(t == 0 || t == 20 ? Square : None, None);
        Assert.Equal(2, d.Hits.Count);
        Assert.Equal(25, d.Hits[1].Tick);
        Assert.Equal(140, d.Two.PositionCm); // 40 of the first 60 cm slid in 20 ticks (larger steps first)
        Assert.Equal(20 + 60, d.PushLeftCm(Side.Two));
        for (int i = 0; i < 40; i++) d.Step(None, None);
        Assert.Equal(220, d.Two.PositionCm);
        Assert.Equal(0, d.PushLeftCm(Side.Two));
    }

    [Fact]
    public void Hyper_armor_holds_its_ground()
    {
        // Two throws an empowered, armored heavy move (30 startup) and takes the shove during its wind-up.
        var armored = WithKnockback(Make.Move("armored", Weight.Heavy, startup: 30, active: 3, recovery: 10, window: 0, perfect: 0,
            stamina: 0, reach: 50), 0, empowerQi: 10);
        var d = new Duel(RulesWith(), Holding(Shove), Holding(armored), 0, 100, oneQi: 0, twoQi: 50);
        for (int t = 0; t <= 20; t++) d.Step(t == 0 ? Square : None, t == 0 ? Square : None);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.DefenderArmored);
        Assert.Equal(0, hit.KnockbackCm);
        Assert.Equal(100, d.Two.PositionCm);
    }

    [Fact]
    public void A_parried_hit_pushes_nobody()
    {
        var d = new Duel(RulesWith(), Holding(Shove), Holding(Parry, Parry), 0, 100);
        // Parry pressed on tick 2: its window opens on frame 3 = tick 5, when the shove lands.
        for (int t = 0; t <= 20; t++) d.Step(t == 0 ? Square : None, t == 2 ? SlotInput.Press(Slot.L1) : None);
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(0, hit.KnockbackCm);
        Assert.Equal(0, d.One.PositionCm);
        Assert.Equal(100, d.Two.PositionCm);
    }

    [Fact]
    public void A_same_weight_clash_pushes_both_apart()
    {
        // Two medium moves. Two starts a 10-frame wind-up on tick 0 (clash window frames 6-9, no perfect frames);
        // One starts the shove on tick 2, so it lands on tick 7, Two's frame 7.
        var slow = WithKnockback(Make.Move("slow", startup: 10, active: 3, recovery: 10, window: 4, perfect: 0, stamina: 0, reach: 200), 60);
        var d = new Duel(RulesWith(recoilPush: 25), Holding(Shove), Holding(slow), 0, 100);
        for (int t = 0; t <= 30; t++) d.Step(t == 2 ? Square : None, t == 0 ? Square : None);
        var hit = d.Hits[0];
        Assert.Equal(ClashOutcome.BothRecoil, hit.Clash);
        Assert.Equal(25, hit.KnockbackCm);
        Assert.Equal(-25, d.One.PositionCm);
        Assert.Equal(125, d.Two.PositionCm);
    }

    [Fact]
    public void Nothing_slides_during_hit_stop()
    {
        var feel = FeelCsv.Parse("event,hitstop,shake,push,spark,sound\n" +
            string.Concat(FeelSheet.EventNames.Keys.Select(n => $"{n},6,0,0,0,none\n"))).Sheet!;
        var clock = new FeelClock(new Duel(RulesWith(), Holding(Shove), Holding(Shove), 0, 100), feel);
        for (int t = 0; t <= 5; t++) clock.Step(t == 0 ? Square : None, None);
        for (int i = 0; i < 6; i++)
        {
            Assert.True(clock.IsFrozen);
            clock.Step(None, None);
            Assert.Equal(100, clock.Duel.Two.PositionCm);
        }
        clock.Step(None, None);
        Assert.Equal(108, clock.Duel.Two.PositionCm);
    }

    // ---------- The real sheets ----------

    private static Dictionary<string, Technique> Sheet() =>
        TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques.ToDictionary(t => t.Id);

    private static Rules SheetRules() => RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;

    /// <summary>The full 罗汉拳 string chained on the first frame, from <paramref name="distance"/> cm.</summary>
    private static Duel RunString(int distance)
    {
        var sheet = Sheet();
        var book = new TechniqueBook(sheet.Values);
        var one = new Loadout(book);
        one.Assign(Slot.Square, sheet["luohan_quan_1"]);
        var d = new Duel(SheetRules(), one, new Loadout(book), 0, distance);
        // Mash every other tick: the buffer chains each hit as early as it can.
        for (int t = 0; t < 200; t++) d.Step(t % 2 == 0 ? Square : None, None);
        return d;
    }

    [Fact]
    public void The_whole_string_stays_in_reach_from_the_duel_start()
    {
        var d = RunString(SheetRules().StartDistance);
        Assert.Equal(new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" }, d.Hits.Select(h => h.Technique.Id));
        int pushed = Sheet().Values.Where(t => t.Id.StartsWith("luohan_quan_")).Sum(t => t.KnockbackCm);
        Assert.Equal(SheetRules().StartDistance + pushed, d.DistanceCm);
    }

    [Fact]
    public void The_reach_limit_the_data_check_prints_is_exact()
    {
        var sheet = Sheet();
        int limit = sheet["luohan_quan_1"].StringReachFrom(id => sheet.TryGetValue(id, out var t) ? t : null);
        Assert.Equal(116, limit);
        Assert.Equal(4, RunString(limit).Hits.Count);
        Assert.True(RunString(limit + 1).Hits.Count < 4);
    }

    [Fact]
    public void The_finisher_ends_the_string_out_of_reach()
    {
        // After the finisher's knockback, hit 1 can't reach: the string has a clear end.
        var d = RunString(SheetRules().StartDistance);
        Assert.True(d.DistanceCm > Sheet()["luohan_quan_1"].ReachCm);
    }

    // ---------- The sheet column ----------

    private const string Header =
        "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry";

    [Fact]
    public void Knockback_is_optional_and_defaults_to_0()
    {
        var r = TechniqueCsv.Parse(Header + "\npunch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(0, r.Techniques[0].KnockbackCm);

        r = TechniqueCsv.Parse(Header + ",knockback\npunch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0,35\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(35, r.Techniques[0].KnockbackCm);
    }

    [Theory]
    [InlineData("punch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0,501", "'knockback' (501) must be 500 cm or less")]
    [InlineData("punch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0,-1", "'knockback' must be a whole number 0 or more")]
    [InlineData("guard,guard,huajin,light,false,3,10,10,0,0,8,0,0,false,0,0,,0,4,20", "'knockback' is set but 'reach' is 0")]
    public void Bad_knockback_is_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + ",knockback\n" + row + "\n");
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Knockback_frames_must_be_at_least_1()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), ("knockback_frames", "0")));
        Assert.Contains(r.Errors, e => e.Contains("knockback_frames") && e.Contains("at least 1"));
    }
}
