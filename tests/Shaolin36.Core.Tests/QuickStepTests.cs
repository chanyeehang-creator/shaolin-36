using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// The quick step: double-tap one of the four directions for a short, fast step that way, relative to the lock-on.
/// Anyone can do it whenever they're free. It costs a little stamina. Its slide has quick_step_invincible invincible frames
/// (Chan: the whole slide, fewer than the 轻功 dash's), its recovery none, and a strike it slips earns nothing (no just dodge).
/// </summary>
public class QuickStepTests
{
    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);
    private static MoveInput Step(StepDirection d) => MoveInput.Step(d);
    private static readonly MoveInput Stand = MoveInput.None;

    // Lands on frame 10, reach 120 cm, a 60-degree wedge.
    private static readonly Technique Jab = Make.Move("jab", startup: 10, active: 3, recovery: 12, window: 0, perfect: 0,
        stamina: 0, damage: 5, hitstun: 20, reach: 120, hitAngle: 60);

    /// <summary>70 cm (90 to the side) over 8 frames, 6 recovery, 6 stamina, like the sheet.</summary>
    private static Rules RulesWith(params (string key, string value)[] extra)
    {
        var all = new List<(string, string)>
        {
            ("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), ("hp_max", "1000"), ("stamina_max", "100"),
            ("stamina_regen_delay", "0"), ("min_distance", "80"), ("start_distance", "110"), ("arena_width", "1400"),
            ("knockback_frames", "8"), ("quick_step_distance", "70"), ("quick_step_frames", "8"),
            ("quick_step_recovery", "6"), ("quick_step_stamina", "6"), ("quick_step_tap_window", "14"),
            ("quick_step_side_distance", "90"),
        };
        all.AddRange(extra);
        var r = RulesCsv.Parse(Make.RulesText(all.ToArray()));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique t)
    {
        var l = new Loadout(new TechniqueBook(new[] { t }));
        l.Assign(Slot.Square, t);
        return l;
    }

    private static Duel NewDuel(int distance = 110, Rules? rules = null) =>
        new(rules ?? RulesWith(), Holding(Jab), Holding(Jab), 0, distance);

    // ---------- Double tap ----------

    private static List<StepDirection> Feed(DoubleTap tap, params StepDirection[] held)
    {
        var fired = new List<StepDirection>();
        foreach (var h in held) fired.Add(tap.Update(h));
        return fired;
    }

    private const StepDirection N = StepDirection.None, F = StepDirection.Forward, B = StepDirection.Back,
        L = StepDirection.Left, R = StepDirection.Right;

    [Fact]
    public void Two_quick_taps_of_the_same_direction_make_a_step_on_the_second_press()
    {
        var fired = Feed(new DoubleTap(14), F, F, N, N, F, F);
        Assert.Equal(new[] { N, N, N, N, F, N }, fired);
    }

    [Fact]
    public void Holding_a_direction_is_one_tap()
    {
        var held = Enumerable.Repeat(R, 40).ToArray();
        Assert.All(Feed(new DoubleTap(14), held), d => Assert.Equal(N, d));
    }

    [Fact]
    public void The_second_tap_must_come_within_the_window()
    {
        // Presses on tick 0 and tick 14: 14 ticks apart, just in.
        var inTime = new[] { L }.Concat(Enumerable.Repeat(N, 13)).Concat(new[] { L }).ToArray();
        Assert.Equal(L, Feed(new DoubleTap(14), inTime).Last());
        // Tick 0 and tick 15: too slow, that's just two walks.
        var late = new[] { L }.Concat(Enumerable.Repeat(N, 14)).Concat(new[] { L }).ToArray();
        Assert.Equal(N, Feed(new DoubleTap(14), late).Last());
    }

    [Fact]
    public void Different_directions_dont_pair_up()
    {
        Assert.All(Feed(new DoubleTap(14), F, N, R, N, F, N, B), d => Assert.Equal(N, d));
    }

    [Fact]
    public void A_third_tap_starts_a_new_pair()
    {
        var fired = Feed(new DoubleTap(14), B, N, B, N, B, N, B);
        Assert.Equal(new[] { N, N, B, N, N, N, B }, fired);
    }

    [Theory]
    [InlineData(100, 0, StepDirection.Forward)]
    [InlineData(-100, 0, StepDirection.Back)]
    [InlineData(0, 100, StepDirection.Right)]
    [InlineData(-30, -90, StepDirection.Left)]
    [InlineData(100, 100, StepDirection.None)] // a diagonal: no step
    [InlineData(40, 0, StepDirection.None)]    // a light push of the stick
    public void Only_the_four_directions_count(int forward, int right, StepDirection expected)
    {
        Assert.Equal(expected, DoubleTap.Cardinal(new MoveInput(forward, right)));
    }

    // ---------- The step ----------

    [Fact]
    public void A_back_step_slides_70_cm_over_8_frames_larger_steps_first()
    {
        var d = NewDuel(200);
        var moved = new List<int>();
        for (int t = 0; t < 12; t++)
        {
            int before = d.One.XCm;
            d.Step(None, None, t == 0 ? Step(B) : Stand, Stand);
            moved.Add(before - d.One.XCm);
        }
        Assert.Equal(new[] { 9, 9, 9, 9, 9, 9, 8, 8, 0, 0, 0, 0 }, moved);
        Assert.Equal(-70, d.One.XCm);
    }

    [Fact]
    public void It_lasts_its_slide_and_recovery_then_you_are_free()
    {
        var d = NewDuel(200);
        d.Step(None, None, Step(F), Stand);
        Assert.Equal(FighterState.QuickStep, d.One.State);
        Assert.Equal(StepDirection.Forward, d.One.QuickStepDirection);
        for (int t = 1; t < 14; t++)
        {
            d.Step(Square, None); // busy: the press waits in the buffer
            Assert.Equal(FighterState.QuickStep, d.One.State);
        }
        d.Step(None, None); // frame 14: free, and the buffered jab comes out
        Assert.Equal(FighterState.Startup, d.One.State);
    }

    [Fact]
    public void It_costs_a_little_stamina_and_none_comes_back_during_it()
    {
        var d = NewDuel(200);
        d.Step(None, None, Step(R), Stand);
        Assert.Equal(94, d.One.Stamina);
        for (int t = 1; t < 14; t++) d.Step(None, None);
        Assert.Equal(94, d.One.Stamina);
    }

    [Fact]
    public void You_cant_quick_step_while_exhausted()
    {
        var d = NewDuel(200);
        d.One.LoseStamina(100);
        d.Step(None, None, Step(B), Stand);
        Assert.Equal(FighterState.Idle, d.One.State);
        Assert.False(d.One.CanQuickStep);
    }

    [Fact]
    public void A_step_asked_for_while_busy_comes_out_when_free_within_the_buffer()
    {
        var d = NewDuel(400);
        d.Step(Square, None); // jab: 25 frames
        for (int t = 1; t < 22; t++) d.Step(None, None);
        d.Step(None, None, Step(B), Stand); // 3 frames early
        d.Step(None, None);
        d.Step(None, None);
        d.Step(None, None); // free on frame 25
        Assert.Equal(FighterState.QuickStep, d.One.State);
    }

    [Fact]
    public void A_forward_step_stops_at_min_distance()
    {
        var d = NewDuel(110);
        for (int t = 0; t < 14; t++) d.Step(None, None, t == 0 ? Step(F) : Stand, Stand);
        Assert.Equal(80, d.DistanceCm);
        Assert.Equal(110, d.Two.XCm);
    }

    [Fact]
    public void A_side_step_curves_round_the_other_fighter()
    {
        var d = NewDuel(110);
        for (int t = 0; t < 14; t++) d.Step(None, None, t == 0 ? Step(R) : Stand, Stand);
        // About 90 cm round to the right (+Z for the player at the start), still about as far away.
        Assert.InRange(d.One.ZCm, 75, 90);
        Assert.InRange(d.DistanceCm, 110, 130);
        Assert.True(d.One.XCm > 0); // it curved toward Two's side rather than going straight
    }

    [Fact]
    public void Side_steps_go_farther_than_forward_and_back()
    {
        int Moved(StepDirection way)
        {
            var d = NewDuel(400);
            var start = d.One.Position;
            for (int t = 0; t < 14; t++) d.Step(None, None, t == 0 ? Step(way) : Stand, Stand);
            return FloorPoint.DistanceCm(start, d.One.Position);
        }
        Assert.Equal(70, Moved(StepDirection.Back));
        Assert.Equal(70, Moved(StepDirection.Forward));
        Assert.InRange(Moved(StepDirection.Left), 88, 90); // 90 cm along a slight curve
        Assert.InRange(Moved(StepDirection.Right), 88, 90);
    }

    [Fact]
    public void The_step_goes_the_same_way_whatever_the_walk_says()
    {
        var d = NewDuel(300);
        d.Step(None, None, new MoveInput(100, 0, StepDirection.Back), Stand);
        for (int t = 1; t < 14; t++) d.Step(None, None, MoveInput.WalkForward, Stand);
        Assert.Equal(-70, d.One.XCm);
    }

    // ---------- Invincible slide, open recovery ----------

    [Fact]
    public void With_no_invincible_frames_a_strike_that_reaches_you_mid_step_lands_and_stops_the_step()
    {
        var d = NewDuel(90);
        d.Step(None, Square);
        for (int t = 1; t < 8; t++) d.Step(None, None);
        d.Step(None, None, Step(B), Stand); // too late: 3 frames from the hit, only 27 cm back (117 cm, inside the 120 reach)
        d.Step(None, None);
        d.Step(None, None);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        int x = d.One.XCm;
        d.Step(None, None);
        Assert.Equal(x, d.One.XCm); // the rest of the step is dropped
        Assert.Equal(0, d.QuickStepLeftCm(Side.One));
    }

    /// <summary>The jab from 110 cm lands on tick 10; One quick-steps forward (to 80 cm, still in reach) on <paramref name="stepTick"/>.</summary>
    private static (Duel duel, int qiBefore) StepIntoJab(int stepTick, int invincible)
    {
        var d = NewDuel(110, RulesWith(("quick_step_invincible", invincible.ToString())));
        int qi = d.One.Qi;
        for (int t = 0; t < 16; t++) d.Step(None, t == 0 ? Square : None, t == stepTick ? Step(F) : Stand, Stand);
        return (d, qi);
    }

    [Fact]
    public void A_strike_landing_in_the_invincible_slide_passes_through_and_earns_nothing()
    {
        // Stepped on tick 5: the jab lands on the step's 6th frame, inside its 8 invincible frames.
        var (d, qi) = StepIntoJab(stepTick: 5, invincible: 8);
        Assert.Empty(d.Hits); // no hit, and no just dodge either
        Assert.Equal(qi, d.One.Qi);
        Assert.Equal(d.One.MaxHp, d.One.Hp);
    }

    [Fact]
    public void A_strike_landing_in_the_step_recovery_hits()
    {
        // Stepped on tick 0: the jab lands on the step's 11th frame, in its recovery (frames 9 to 14).
        var (d, _) = StepIntoJab(stepTick: 0, invincible: 8);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
    }

    [Fact]
    public void Only_the_first_quick_step_invincible_frames_are_invincible()
    {
        var d = NewDuel(200, RulesWith(("quick_step_invincible", "5")));
        d.Step(None, None, Step(B), Stand);
        var seen = new List<bool> { d.One.IsInvincible };
        for (int t = 1; t < 14; t++)
        {
            d.Step(None, None);
            seen.Add(d.One.IsInvincible);
        }
        Assert.Equal(new[] { true, true, true, true, true, false, false, false, false, false, false, false, false, false }, seen);
    }

    [Fact]
    public void The_recovery_is_never_invincible()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"),
            ("quick_step_frames", "8"), ("quick_step_invincible", "9")));
        Assert.Contains(r.Errors, e => e.Contains("quick_step_invincible"));
    }

    [Fact]
    public void On_the_sheet_the_whole_slide_is_invincible_and_shorter_than_the_dash()
    {
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var dash = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques.First(t => t.Id == "qinggong_dash");
        Assert.Equal(rules.QuickStepFrames, rules.QuickStepInvincible); // Chan: the whole step but not the recovery
        Assert.True(rules.QuickStepInvincible < dash.InvincibleFrames);
    }

    // ---------- What it's for ----------

    /// <summary>The dummy's 罗汉拳 from 110 cm; the player quick-steps on <paramref name="stepTick"/>.</summary>
    private static Duel DodgeLuohan(StepDirection way, int stepTick)
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var dummy = new Loadout(book);
        dummy.Assign(Slot.Square, book.Find("luohan_quan_1")!);
        var d = new Duel(rules, new Loadout(book), dummy, -55, 55); // 110 cm apart, in 罗汉拳 reach
        for (int t = 0; t < 20; t++)
            d.Step(None, t == 0 ? Square : None, t == stepTick ? Step(way) : Stand, Stand);
        return d;
    }

    [Theory]
    [InlineData(StepDirection.Right)]
    [InlineData(StepDirection.Left)]
    [InlineData(StepDirection.Back)]
    public void A_quick_step_as_the_wind_up_starts_slips_the_first_罗汉拳_punch(StepDirection way)
    {
        Assert.Empty(DodgeLuohan(way, stepTick: 0).Hits);
    }

    [Fact]
    public void After_a_side_step_the_dummys_whole_string_misses()
    {
        // Chan's playtest: the dummy's string keeps the aim of its first punch, so slipping it slips them all.
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var loadout = new Loadout(book);
        loadout.Assign(Slot.Square, book.Find("luohan_quan_1")!);
        var d = new Duel(rules, new Loadout(book), loadout, -55, 55); // 110 cm apart, in 罗汉拳 reach
        var dummy = TrainingDummy.FromRules(rules, Slot.Square);
        bool stepped = false;
        int deepest = 0;
        for (int t = 0; t < rules.DummyPause + 90; t++)
        {
            var press = dummy.Think(d.Two);
            var move = !stepped && press.Any ? Step(StepDirection.Right) : Stand;
            stepped |= press.Any;
            d.Step(None, press, move, Stand);
            deepest = Math.Max(deepest, d.Two.StringHit);
        }
        Assert.Equal(4, deepest); // the dummy threw all four hits
        Assert.Empty(d.Hits);
    }

    [Fact]
    public void Stepping_just_before_the_punch_slips_it_on_the_invincible_frames()
    {
        // 罗汉拳 hit 1 comes out on frame 13; a step on frame 10 hasn't moved far enough, but the slide is invincible.
        Assert.Empty(DodgeLuohan(StepDirection.Right, stepTick: 10).Hits);
    }

    [Fact]
    public void After_a_side_step_you_are_off_the_dummys_line_and_your_punch_reaches()
    {
        var d = DodgeLuohan(StepDirection.Right, stepTick: 0);
        var punch = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques
            .First(t => t.Id == "luohan_quan_1");
        Assert.True(d.DistanceCm <= punch.ReachCm + punch.AdvanceCm);
        Assert.False(punch.Covers(d.Two.Facing, d.One.Position - d.Two.Position)); // outside its punch's wedge
    }

    // ---------- Hit-stop and the sheet ----------

    [Fact]
    public void A_quick_step_asked_for_during_hit_stop_comes_out_straight_after()
    {
        var feel = FeelCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("feel.csv"))).Sheet!;
        var clock = new FeelClock(NewDuel(100), feel);
        for (int t = 0; t <= 10; t++) clock.Step(None, t == 0 ? Square : None);
        Assert.True(clock.IsFrozen);
        clock.Step(None, None, Step(StepDirection.Back), Stand);
        while (clock.IsFrozen) clock.Step(None, None);
        clock.Step(None, None);
        // The step wasn't lost in the freeze: One is in hitstun, so it waits in the input buffer like a slot press.
        Assert.Equal(FighterState.Hitstun, clock.Duel.One.State);
        Assert.Equal(StepDirection.Back, clock.Duel.One.BufferedStep);
    }

    [Theory]
    [InlineData("quick_step_tap_window", "0", "between 1 and 30")]
    [InlineData("quick_step_tap_window", "31", "between 1 and 30")]
    [InlineData("quick_step_distance", "301", "300 cm or less")]
    [InlineData("quick_step_frames", "0", "at least 1 frame")]
    public void Bad_quick_step_numbers_are_reported(string key, string value, string expected)
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), (key, value)));
        Assert.Contains(r.Errors, e => e.Contains(key) && e.Contains(expected));
    }

    [Fact]
    public void The_sheets_quick_step_is_short_cheap_and_quick()
    {
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        Assert.InRange(rules.QuickStepDistance, 40, 100);              // a step, not a roll
        Assert.InRange(rules.QuickStepStamina, 1, rules.StaminaMax / 10);
        Assert.InRange(rules.QuickStepFrames + rules.QuickStepRecovery, 8, 24);
    }
}
