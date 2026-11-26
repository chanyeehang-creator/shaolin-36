using Shaolin36.Core.Runs;
using System.IO;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Chan: the game is single-player and never PvP, so the player gets the edge (<see cref="Duel.FavourOne"/>). A side quick step
/// loses the opponent for a moment, opponents walk back slower than you walk forward, 罗汉拳 and 虎形拳 cost about 30% less stamina
/// and you start a run with 100, a string that connects keeps facing its target, you (and only the boss) can block out of hitstun,
/// you can 纵云梯 out of hitstun, and the jump rises out of every strike's reach at the top.
/// </summary>
public class PlayerEdgeTests
{
    private static Rules RulesWith(params (string key, string value)[] set) => RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l =>
        {
            foreach (var (k, v) in set) if (l.StartsWith(k + ",")) return $"{k},{v},test";
            return l;
        }))).Rules!;

    private static readonly Rules Rules = RulesWith(("opponent_ready", "0"));
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Technique T(string id) => Book.Find(id)!;

    private static Loadout Set()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("luohan_quan_1"));
        l.Assign(Slot.Cross, T("zongyun_ti"));
        l.Assign(Slot.L1, T("timed_parry"));
        return l;
    }

    private static Duel Favoured(int apart = 110, bool boss = false, Rules? rules = null, Loadout? one = null, Loadout? two = null)
    {
        var d = new Duel(rules ?? Rules, one ?? Set(), two ?? Set(), 0, apart);
        d.Two.CanLoseLock = true;
        d.Two.RelockFrames = 0; // the master: finds you as soon as it stands still after a jump
        d.FavourOne(boss);
        return d;
    }

    private static void Idle(Duel d, int ticks, MoveInput one = default, MoveInput two = default)
    {
        for (int i = 0; i < ticks; i++) d.Step(SlotInput.None, SlotInput.None, one, two);
    }

    // ---------- Sideways quick step ----------

    [Theory]
    [InlineData(StepDirection.Left)]
    [InlineData(StepDirection.Right)]
    public void A_side_quick_step_loses_the_opponent_for_side_step_lose_frames(StepDirection way)
    {
        var d = Favoured();
        d.Step(SlotInput.None, SlotInput.None, MoveInput.Step(way), MoveInput.None);
        Assert.False(d.Two.IsLockedOn);
        // Standing still doesn't find you early (even the master): it is a timed loss, counted from the step's first tick.
        Idle(d, Rules.SideStepLoseFrames - 2);
        Assert.False(d.Two.IsLockedOn);
        d.Step(SlotInput.None, SlotInput.None);
        Assert.True(d.Two.IsLockedOn);
        Assert.True(d.Two.IsTurning, "it turns back round to you instead of snapping");
    }

    [Theory]
    [InlineData(StepDirection.Forward)]
    [InlineData(StepDirection.Back)]
    public void A_forward_or_back_quick_step_keeps_the_opponent_tracking(StepDirection way)
    {
        var d = Favoured(150);
        d.Step(SlotInput.None, SlotInput.None, MoveInput.Step(way), MoveInput.None);
        Idle(d, 3);
        Assert.True(d.Two.IsLockedOn);
    }

    [Fact]
    public void While_lost_after_a_side_step_the_opponent_doesnt_turn_to_follow_you()
    {
        var d = Favoured();
        var facing = d.Two.Facing;
        d.Step(SlotInput.None, SlotInput.None, MoveInput.Step(StepDirection.Right), MoveInput.None);
        Idle(d, 14);
        Assert.Equal(facing, d.Two.Facing);
        Assert.NotEqual(0, d.One.ZCm);
    }

    [Fact]
    public void The_opponents_own_side_step_never_loses_you()
    {
        var d = Favoured();
        d.Step(SlotInput.None, SlotInput.None, MoveInput.None, MoveInput.Step(StepDirection.Left));
        Assert.True(d.One.IsLockedOn);
    }

    [Fact]
    public void Hitting_the_lost_opponent_finds_you_at_once()
    {
        var d = Favoured(100);
        d.Step(SlotInput.None, SlotInput.None, MoveInput.Step(StepDirection.Left), MoveInput.None);
        Idle(d, Rules.QuickStepFrames + Rules.QuickStepRecovery);
        Assert.False(d.Two.IsLockedOn);
        d.Step(SlotInput.Press(Slot.Square), SlotInput.None);
        for (int i = 0; i < 20 && !d.Two.IsLockedOn; i++) d.Step(SlotInput.None, SlotInput.None);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        Assert.True(d.Two.IsLockedOn);
    }

    // ---------- Walking speeds ----------

    [Fact]
    public void Your_back_walk_keeps_pace_with_the_opponents_forward_walk()
    {
        var d = Favoured(400);
        Idle(d, 60, MoveInput.WalkBack, MoveInput.WalkForward);
        // Both walk walk_speed: the gap stays the same.
        Assert.InRange(d.DistanceCm, 399, 401);
        Assert.Equal(-Rules.WalkSpeed, d.One.XCm);
        Assert.Equal(400 - Rules.WalkSpeed, d.Two.XCm);
    }

    [Fact]
    public void The_opponent_walks_back_at_cpu_walk_back_pct_so_you_catch_it()
    {
        Assert.Equal(90, Rules.CpuWalkBackPct);
        var d = Favoured(400);
        Idle(d, 60, MoveInput.WalkForward, MoveInput.WalkBack);
        Assert.Equal(Rules.WalkSpeed, d.One.XCm);
        Assert.Equal(400 + Rules.WalkSpeed * Rules.CpuWalkBackPct / 100, d.Two.XCm);
        Assert.True(d.DistanceCm < 400, "you close the gap");
    }

    [Fact]
    public void You_walk_back_at_full_speed()
    {
        var d = Favoured(400);
        Idle(d, 60, MoveInput.WalkBack);
        Assert.Equal(-Rules.WalkSpeed, d.One.XCm);
    }

    // ---------- Stamina ----------

    [Fact]
    public void Luohan_quan_and_huxing_quan_cost_about_30_percent_less()
    {
        Assert.Equal(new[] { 6, 6, 6, 10 }, new[] { 1, 2, 3, 4 }.Select(i => T($"luohan_quan_{i}").StaminaCost).ToArray());
        Assert.Equal(new[] { 6, 6, 8 }, new[] { 1, 2, 3 }.Select(i => T($"huxing_quan_{i}").StaminaCost).ToArray());
        // Was 39 for the 罗汉拳 string and 30 for 虎形拳.
        Assert.Equal(28, new[] { 1, 2, 3, 4 }.Sum(i => T($"luohan_quan_{i}").StaminaCost));
        Assert.Equal(20, new[] { 1, 2, 3 }.Sum(i => T($"huxing_quan_{i}").StaminaCost));
    }

    // ---------- Facing your target once a hit connects ----------

    /// <summary>
    /// One throws 罗汉拳 hit 1 at Two from 100 cm (Two parries it when <paramref name="parried"/>), then Two slides 60 cm to the side
    /// and One chains hit 2. Returns how squarely One faces Two partway through hit 2's wind-up.
    /// </summary>
    private static double FacingAfterChain(bool favoured, bool parried = false, bool whiff = false)
    {
        var d = new Duel(Rules, Set(), Set(), 0, whiff ? 400 : 100);
        if (favoured) d.FavourOne(false);
        var luohan = T("luohan_quan_1");
        int hitTick = luohan.StartupFrames;
        // Pressed so the hit meets the parry's first block frames (a plain block, before the perfect frames).
        int parryAt = hitTick - T("timed_parry").StartupFrames - 1;
        for (int i = 0; i <= hitTick; i++)
            d.Step(i == 0 ? SlotInput.Press(Slot.Square) : SlotInput.None, parried && i == parryAt ? SlotInput.Press(Slot.L1) : SlotInput.None);
        Assert.Equal(whiff ? 0 : 1, d.Hits.Count);
        if (parried) Assert.Equal(ParryOutcome.Parried, d.Hits[0].Parry);
        d.Two.Position = d.Two.Position + FloorPoint.FromCm(0, 60);
        // Chain on the first chain frame.
        for (int guard = 0; guard < 300 && !d.One.InChainWindow; guard++) d.Step(SlotInput.None, SlotInput.None);
        d.Step(SlotInput.Press(Slot.Square), SlotInput.None);
        Assert.Equal(2, d.One.StringHit);
        for (int i = 0; i < 5; i++) d.Step(SlotInput.None, SlotInput.None);
        var to = d.Two.Position - d.One.Position;
        return FloorPoint.Dot(d.One.Facing, to) / (d.One.Facing.Length * to.Length);
    }

    [Fact]
    public void Once_hit_1_lands_hit_2_turns_to_face_the_target()
    {
        Assert.True(FacingAfterChain(true) > 0.999);
        // Without the edge (the opponents), a string keeps the aim it started with.
        Assert.True(FacingAfterChain(false) < 0.95);
    }

    [Fact]
    public void A_blocked_first_hit_still_counts_as_connecting()
    {
        Assert.True(FacingAfterChain(true, parried: true) > 0.999);
    }

    [Fact]
    public void A_whiffed_first_hit_does_not_turn_the_string()
    {
        Assert.True(FacingAfterChain(true, whiff: true) < 0.999);
    }

    // ---------- Out of hitstun: 防身 and 纵云梯 ----------

    /// <summary>Puts One in hitstun from Two's 罗汉拳 hit 1 (from 100 cm) and returns the duel on the tick after the hit.</summary>
    private static Duel OneInHitstun(bool favoured = true)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 100);
        if (favoured) d.FavourOne(false);
        d.Step(SlotInput.None, SlotInput.Press(Slot.Square));
        for (int guard = 0; guard < 300 && d.One.State != FighterState.Hitstun; guard++) d.Step(SlotInput.None, SlotInput.None);
        d.Step(SlotInput.None, SlotInput.None);
        return d;
    }

    [Fact]
    public void You_can_raise_huamian_zhang_in_hitstun()
    {
        var d = OneInHitstun();
        d.Step(SlotInput.Press(Slot.L1), SlotInput.None);
        Assert.Equal("timed_parry", d.One.Technique?.Id);
    }

    [Fact]
    public void You_can_raise_yingqi_gong_in_hitstun()
    {
        var l = Set();
        l.Assign(Slot.L1, T("yingqi_gong"));
        var d = new Duel(Rules, l, Set(), 0, 100);
        d.FavourOne(false);
        d.Step(SlotInput.None, SlotInput.Press(Slot.Square));
        for (int guard = 0; guard < 300 && d.One.State != FighterState.Hitstun; guard++) d.Step(SlotInput.None, SlotInput.None);
        d.Step(SlotInput.Press(Slot.L1).Holding(Slot.L1), SlotInput.None);
        Assert.Equal("yingqi_gong", d.One.Technique?.Id);
    }

    [Fact]
    public void You_can_jump_out_of_hitstun_with_zongyun_ti()
    {
        var d = OneInHitstun();
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.None);
        Assert.True(d.One.IsAirborne);
        Assert.True(d.One.EscapedHitstun);
    }

    [Fact]
    public void Without_the_edge_hitstun_only_lets_the_dash_out()
    {
        var d = OneInHitstun(favoured: false);
        d.Step(SlotInput.Press(Slot.L1), SlotInput.None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    /// <summary>One hits Two with 罗汉拳 hit 1; on the tick after, Two (hitstun) presses <paramref name="slot"/>.</summary>
    private static Duel TwoPressesInHitstun(bool boss, Slot slot)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 100);
        d.FavourOne(boss);
        d.Step(SlotInput.Press(Slot.Square), SlotInput.None);
        for (int guard = 0; guard < 300 && d.Two.State != FighterState.Hitstun; guard++) d.Step(SlotInput.None, SlotInput.None);
        d.Step(SlotInput.None, SlotInput.Press(slot));
        return d;
    }

    [Fact]
    public void Only_the_boss_can_block_out_of_hitstun()
    {
        Assert.Equal("timed_parry", TwoPressesInHitstun(boss: true, Slot.L1).Two.Technique?.Id);
        Assert.Equal(FighterState.Hitstun, TwoPressesInHitstun(boss: false, Slot.L1).Two.State);
    }

    [Fact]
    public void No_opponent_jumps_out_of_hitstun_not_even_the_boss()
    {
        Assert.Equal(FighterState.Hitstun, TwoPressesInHitstun(boss: true, Slot.Cross).Two.State);
    }

    [Fact]
    public void The_boss_brain_parries_the_next_hit_of_your_combo_from_hitstun()
    {
        Assert.True(SecondHitParried(boss: true));
        Assert.False(SecondHitParried(boss: false));
    }

    /// <summary>
    /// One lands 罗汉拳 hit 1 on a brain that always parries what it sees (reaction 0), then chains hit 2 while it is still in hitstun.
    /// True if hit 2 was parried.
    /// </summary>
    private static bool SecondHitParried(bool boss)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 100);
        d.FavourOne(boss);
        var brain = new Brain(new BrainStyle("test", "test", 0, 10, 0, 0, 0, 100, 100, 0, 0, 0, 0), 1) { Active = false };
        for (int i = 0; i < 200 && d.Hits.Count < 2 && !d.IsOver; i++)
        {
            if (d.Hits.Count == 1) brain.Active = true;
            var b = brain.Think(d, Side.Two);
            var one = d.One.InChainWindow && d.One.StringHit == 1 || i == 0 ? SlotInput.Press(Slot.Square) : SlotInput.None;
            d.Step(one, b.Slots, MoveInput.None, b.Move);
        }
        Assert.True(d.Hits[0].Landed, "hit 1 landed");
        return d.Hits.Count >= 2 && d.Hits[1].Parry != ParryOutcome.NotParried;
    }

    // ---------- 纵云梯 height ----------

    [Fact]
    public void The_jump_rises_to_jump_height_and_comes_back_down()
    {
        var d = Favoured(400);
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.None, MoveInput.WalkBack, MoveInput.None);
        int top = 0;
        bool above = false;
        int lowAtStart = d.HeightCm(d.One);
        for (int guard = 0; guard < 300 && d.One.IsAirborne; guard++)
        {
            top = System.Math.Max(top, d.HeightCm(d.One));
            above |= d.IsAboveStrikes(d.One);
            d.Step(SlotInput.None, SlotInput.None);
        }
        Assert.InRange(top, Rules.JumpHeight - 5, Rules.JumpHeight);
        Assert.True(above);
        Assert.True(lowAtStart < Rules.StrikeTop, "low on take-off");
        Assert.Equal(0, d.HeightCm(d.One));
    }

    /// <summary>Two throws a strike that reaches all round to 500 cm, timed so its active frames meet One at the top of a 纵云梯.</summary>
    private static bool StrikeAtTheTopLands(Rules rules)
    {
        var big = Make.Move("big", startup: 14, active: 3, reach: 500);
        var two = new Loadout(Book);
        two.Assign(Slot.Square, big);
        var d = Favoured(200, rules: rules, two: two);
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.Press(Slot.Square), MoveInput.WalkBack, MoveInput.None);
        for (int i = 0; i < 20; i++) d.Step(SlotInput.None, SlotInput.None);
        return d.Hits.Any(h => h.Attacker == Side.Two && h.Landed);
    }

    [Fact]
    public void At_the_top_of_the_jump_no_strike_reaches_you()
    {
        Assert.False(StrikeAtTheTopLands(Rules));
        // With no height (the old jump), the same strike hits you in the air.
        Assert.True(StrikeAtTheTopLands(RulesWith(("opponent_ready", "0"), ("jump_height", "0"))));
    }

    // ---------- Run ----------

    [Fact]
    public void A_run_starts_you_with_100_stamina_the_same_as_hp()
    {
        var sheet = RunCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("run.csv"))).Sheet!;
        Assert.Equal(100, sheet.PlayerStamina);
        Assert.Equal(sheet.PlayerHp, sheet.PlayerStamina);
    }
}

/// <summary>Chan: a cleared chamber (and chamber 0) has no dummy. You walk free the way you face, which the game sets from the camera.</summary>
public class RoamingTests
{
    private static readonly Rules Rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Duel Roam()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.Cross, Book.Find("qinggong_dash")!);
        var d = Duel.Start(Rules, l, new Loadout(Book));
        d.Roaming = true;
        return d;
    }

    [Fact]
    public void You_walk_the_way_you_face_not_at_the_other_fighter()
    {
        var d = Roam();
        d.One.Facing = new FloorPoint(0, 1);
        int x = d.One.XCm;
        for (int i = 0; i < 60; i++) d.Step(SlotInput.None, SlotInput.None, MoveInput.WalkForward, MoveInput.None);
        Assert.Equal(x, d.One.XCm);
        Assert.Equal(Rules.WalkSpeed, d.One.ZCm);
        Assert.Equal(new FloorPoint(0, 1), d.One.Facing);
    }

    [Fact]
    public void You_walk_through_where_the_hidden_fighter_stands_and_never_hit_it()
    {
        var d = Roam();
        var two = d.Two.Position;
        d.One.Facing = new FloorPoint(1, 0);
        for (int i = 0; i < 300; i++)
            d.Step(i == 60 || i == 200 ? SlotInput.Press(Slot.Square) : SlotInput.None, SlotInput.None, MoveInput.WalkForward, MoveInput.None);
        Assert.True(d.One.XCm > d.Two.XCm, "walked past it");
        Assert.Empty(d.Hits);
        Assert.Equal(two, d.Two.Position);
        Assert.Equal(d.Two.MaxHp, d.Two.Hp);
    }

    [Fact]
    public void A_dash_goes_the_way_you_face()
    {
        var d = Roam();
        d.One.Facing = new FloorPoint(0, -1);
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.None);
        for (int i = 0; i < 30; i++) d.Step(SlotInput.None, SlotInput.None);
        Assert.Equal(-Book.Find("qinggong_dash")!.DashCm, d.One.ZCm);
    }

    [Fact]
    public void The_arena_edge_still_stops_you()
    {
        var d = Roam();
        d.One.Facing = new FloorPoint(-1, 0);
        for (int i = 0; i < 600; i++) d.Step(SlotInput.None, SlotInput.None, MoveInput.WalkForward, MoveInput.None);
        Assert.Equal(-Rules.ArenaWidth / 2, d.One.XCm);
    }
}
