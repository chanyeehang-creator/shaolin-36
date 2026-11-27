using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// 外功 attacks step in: a technique's advance moves the attacker forward along its aim, slid over the
/// startup frames, for the player and the dummy alike.
/// </summary>
public class AdvanceTests
{
    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    /// <summary>Startup 6, steps <paramref name="advance"/> cm, reach 100.</summary>
    private static Technique Lunge(int advance = 30, int reach = 100, int hitAngle = 360) =>
        new("lunge", "lunge", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 20, reach,
            hitAngle: hitAngle, advanceCm: advance);

    private static Rules RulesWith(int minDistance = 80)
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"),
            ("hp_max", "1000"), ("min_distance", minDistance.ToString()), ("start_distance", "110"), ("arena_width", "1400"),
            ("knockback_frames", "8")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique t)
    {
        var l = new Loadout(new TechniqueBook(new[] { t }));
        l.Assign(Slot.Square, t);
        return l;
    }

    [Fact]
    public void The_step_slides_over_the_startup_frames_larger_steps_first()
    {
        var d = new Duel(RulesWith(), Holding(Lunge(advance: 32)), Holding(Lunge()), 0, 300);
        var steps = new List<int>();
        for (int t = 0; t < 10; t++)
        {
            int before = d.One.XCm;
            d.Step(t == 0 ? Square : None, None);
            steps.Add(d.One.XCm - before);
        }
        // 32 cm over 6 startup frames, from the tick it starts; nothing after the wind-up.
        Assert.Equal(new[] { 6, 6, 5, 5, 5, 5, 0, 0, 0, 0 }, steps);
        Assert.Equal(32, d.One.XCm);
    }

    [Fact]
    public void The_step_brings_a_target_just_out_of_reach_into_it()
    {
        var still = Lunge(advance: 0);
        var d = new Duel(RulesWith(), Holding(still), Holding(still), 0, 125);
        for (int t = 0; t < 12; t++) d.Step(t == 0 ? Square : None, None);
        Assert.Empty(d.Hits);

        d = new Duel(RulesWith(), Holding(Lunge(advance: 30)), Holding(still), 0, 125);
        for (int t = 0; t < 12; t++) d.Step(t == 0 ? Square : None, None);
        var hit = Assert.Single(d.Hits);
        Assert.Equal(6, hit.Tick);
    }

    [Fact]
    public void The_step_goes_along_the_aim_even_if_the_target_moves_away()
    {
        var d = new Duel(RulesWith(), Holding(Lunge(advance: 30)), Holding(Lunge()), 0, 0);
        d.Two.Position = FloorPoint.FromCm(0, 200); // straight ahead along +Z
        d.Step(None, None);
        d.Step(Square, None);
        d.Two.Position = FloorPoint.FromCm(200, 200);
        for (int t = 0; t < 10; t++) d.Step(None, None);
        Assert.Equal(0, d.One.XCm);
        Assert.Equal(30, d.One.ZCm);
    }

    [Fact]
    public void The_step_stops_at_min_distance()
    {
        var d = new Duel(RulesWith(minDistance: 80), Holding(Lunge(advance: 50)), Holding(Lunge()), 0, 100);
        for (int t = 0; t < 10; t++) d.Step(t == 0 ? Square : None, None);
        Assert.Equal(80, d.DistanceCm);
        Assert.Equal(100, d.Two.XCm); // the defender isn't shoved
    }

    [Fact]
    public void Being_hit_out_of_the_move_stops_the_step()
    {
        // Two's quick jab lands on frame 2 of One's lunge and stuns it: One stops where it is.
        var jab = new Technique("jab", "jab", Category.Waigong, Weight.Medium, false, 2, 2, 10, 0, 0, 0, 0, 5, true, 30, 200);
        var d = new Duel(RulesWith(), Holding(Lunge(advance: 60)), Holding(jab), 0, 150);
        d.Step(Square, Square);
        for (int t = 0; t < 12; t++) d.Step(None, None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        Assert.Equal(30, d.One.XCm); // 3 ticks of 10 cm (frames 0-2), then hitstun
        Assert.Equal(0, d.AdvanceLeftCm(Side.One));
    }

    [Fact]
    public void Each_hit_of_a_string_steps_in_again()
    {
        var two = new Technique("two", "two", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 0, 100,
            advanceCm: 12);
        var one = new Technique("one", "one", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 0, 100,
            next: "two", chainWindowFrames: 6, advanceCm: 10);
        var l = new Loadout(new TechniqueBook(new[] { one, two }));
        l.Assign(Slot.Square, one);
        var d = new Duel(RulesWith(), l, Holding(Lunge()), 0, 400);
        for (int t = 0; t < 40; t++) d.Step(t == 0 || t == 9 ? Square : None, None);
        Assert.Equal(22, d.One.XCm);
    }

    [Fact]
    public void The_dummy_holds_its_ground_in_the_wind_up_and_lunges_on_the_hit()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var dummyLoadout = new Loadout(book);
        dummyLoadout.Assign(Slot.Square, book.Find("luohan_quan_1")!);
        var d = new Duel(rules, new Loadout(book), dummyLoadout, -55, 55); // 110 cm apart, in 罗汉拳 reach
        var dummy = TrainingDummy.FromRules(rules, Slot.Square);
        int start = d.Two.XCm;
        // 罗汉拳 holds its ground through the wind-up (Chan: the punch drives the body forward, not the wind-up)...
        for (int t = 0; t < rules.DummyPause + 13; t++) d.Step(None, dummy.Think(d.Two));
        Assert.Equal(start, d.Two.XCm);
        Assert.Empty(d.Hits);
        // ...then lunges over its 3 active frames, toward the player at -X.
        for (int t = 0; t < 3; t++) d.Step(None, dummy.Think(d.Two));
        Assert.Equal(start - book.Find("luohan_quan_1")!.LungeCm, d.Two.XCm);
        Assert.Single(d.Hits);
    }

    [Fact]
    public void Huxing_quan_holds_the_tiger_pose_still_then_pounces_on_the_hit()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var tiger = book.Find("huxing_quan_1")!;
        var loadout = new Loadout(book);
        loadout.Assign(Slot.Square, tiger);
        var d = new Duel(rules, loadout, new Loadout(book), 0, 240); // farther than its reach: only the pounce gets there
        // Chan: the tiger pose (the wind-up) stands still...
        for (int t = 0; t < tiger.StartupFrames; t++) d.Step(t == 0 ? SlotInput.Press(Slot.Square) : None, None);
        Assert.Equal(0, d.One.XCm);
        // ...then the pounce carries the body and the hit forward together over the active frames.
        for (int t = 0; t < tiger.ActiveFrames && d.Hits.Count == 0; t++) d.Step(None, None);
        Assert.True(d.One.XCm > 100, $"pounced {d.One.XCm} cm");
        Assert.Single(d.Hits);
        Assert.True(d.Hits[0].Landed);
    }

    // ---------- The sheet ----------

    private const string Header =
        "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry";
    private const string Row = "punch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0";

    [Fact]
    public void Advance_is_optional_and_defaults_to_0()
    {
        var r = TechniqueCsv.Parse(Header + "\n" + Row + "\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(0, r.Techniques[0].AdvanceCm);

        r = TechniqueCsv.Parse(Header + ",advance\n" + Row + ",25\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(25, r.Techniques[0].AdvanceCm);
    }

    [Theory]
    [InlineData("301", "'advance' (301) must be 300 cm or less")]
    [InlineData("-5", "'advance' must be a whole number 0 or more")]
    public void Bad_advance_is_reported(string value, string expected)
    {
        var r = TechniqueCsv.Parse(Header + ",advance\n" + Row + "," + value + "\n");
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Every_waigong_strike_on_the_sheet_steps_in_a_little()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        // 虎形拳 leaps (Chan: each hit a leap forward, farther than 罗汉拳), so up to 50; its first hit pounces almost as far
        // as 封穴's lunge, a little less (Chan), and hit 2's wind-up rolls after the opponent that pounce knocked back.
        int lunge = sheet.Single(t => t.Id == "fengxue").LungeCm;
        foreach (var t in sheet.Where(t => t.Category == Category.Waigong))
            Assert.InRange(t.AdvanceCm + t.LungeCm, 5, t.Id == "huxing_quan_1" ? lunge - 1 : t.Id == "huxing_quan_2" ? 120 : t.Id.StartsWith("huxing") ? 50 : 30);
        // 罗汉拳 steps on the hit, not in the wind-up (Chan: the punch or kick drives the body forward).
        foreach (var t in sheet.Where(t => t.Id.StartsWith("luohan_quan_")))
        {
            Assert.Equal(0, t.AdvanceCm);
            Assert.True(t.LungeCm > 0, t.Id);
        }
        // 虎形拳 (Chan, from the reference footage): hit 1 stands in the tiger pose, then pounces on the hit; hit 2 rolls forward
        // in its wind-up; hit 3 stands on one leg charging, then stomps forward on the hit.
        var hu = new[] { 1, 2, 3 }.Select(i => sheet.Single(t => t.Id == $"huxing_quan_{i}")).ToArray();
        Assert.Equal(0, hu[0].AdvanceCm);
        Assert.Equal(0, hu[2].AdvanceCm);
        // The pounce stops where it lands and knocks the opponent back; the roll covers that ground (a little more, so it
        // ends in reach however far out the pounce landed).
        Assert.True(hu[0].LungeStops);
        Assert.InRange(hu[1].AdvanceCm, hu[0].KnockbackCm, hu[0].KnockbackCm + 10);
        Assert.InRange(hu[0].LungeCm, lunge * 3 / 4, lunge - 1);
        Assert.Equal(45, hu[2].LungeCm);
    }

    // ---------- A lunge that stops where it hits (虎形拳 1's pounce) ----------

    /// <summary>Startup 6, active 6, a 120 cm lunge (20 cm a frame), reach 100, knocks back 60.</summary>
    private static Technique Pounce(bool stops) =>
        new("pounce", "pounce", Category.Waigong, Weight.Medium, false, 6, 6, 12, 0, 0, 0, 0, 5, true, 20, 100,
            knockbackCm: 60, lungeCm: 120, lungeStops: stops);

    [Fact]
    public void A_lunge_that_stops_ends_where_its_hit_connects_and_a_plain_one_chases()
    {
        // From 110 cm the lunge's first 20 cm brings the hit into reach: it lands on the first active frame, 90 cm apart.
        var d = new Duel(RulesWith(), Holding(Pounce(stops: true)), Holding(Lunge()), 0, 110);
        for (int t = 0; t < 30; t++) d.Step(t == 0 ? Square : None, None);
        Assert.True(Assert.Single(d.Hits).Landed);
        Assert.Equal(20, d.One.XCm);         // stopped where it hit...
        Assert.Equal(90 + 60, d.DistanceCm); // ...and the knockback opened the gap

        var chasing = new Duel(RulesWith(), Holding(Pounce(stops: false)), Holding(Lunge()), 0, 110);
        for (int t = 0; t < 30; t++) chasing.Step(t == 0 ? Square : None, None);
        Assert.True(chasing.One.XCm > 60, $"a plain lunge carries on after the fighter it knocked back ({chasing.One.XCm} cm)");
        Assert.True(chasing.DistanceCm < 90 + 60);
    }

    [Fact]
    public void A_lunge_that_stops_still_lunges_its_whole_way_when_it_whiffs()
    {
        var d = new Duel(RulesWith(), Holding(Pounce(stops: true)), Holding(Lunge()), 0, 400);
        for (int t = 0; t < 30; t++) d.Step(t == 0 ? Square : None, None);
        Assert.Empty(d.Hits);
        Assert.Equal(120, d.One.XCm);
    }

    [Fact]
    public void The_string_reach_counts_a_stopping_lunge_as_connecting_at_the_edge_of_its_reach()
    {
        // Hit a pounces (reach 100, lunge 100) and knocks back 50; hit b steps in 40 with reach 100.
        Technique A(bool stops) => new("a", "a", Category.Waigong, Weight.Medium, false, 6, 5, 12, 0, 0, 0, 0, 5, true, 30, 100, "b", 6,
            knockbackCm: 50, lungeCm: 100, lungeStops: stops);
        var b = new Technique("b", "b", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 20, 100, advanceCm: 40);
        // Carried all the way, a's lunge closes 100 cm: from 190 cm, b reaches (190 - 100 + 50 - 40 = 100).
        Assert.Equal(190, A(false).StringReachFrom(id => id == "b" ? b : null));
        // Stopping where it hits, a can connect at the edge of its reach and leave 100 + 50 cm: b only reaches if a starts
        // within 90 cm (90 + 50 - 40 = 100).
        Assert.Equal(90, A(true).StringReachFrom(id => id == "b" ? b : null));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Lunge_stops_is_an_optional_column(string value)
    {
        var r = TechniqueCsv.Parse(Header + ",lunge,lunge_stops\n" + Row + ",40," + value + "\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(value == "true", r.Techniques[0].LungeStops);
        Assert.False(TechniqueCsv.Parse(Header + "\n" + Row + "\n").Techniques[0].LungeStops);
    }

    [Fact]
    public void Lunge_stops_without_a_lunge_is_reported()
    {
        var r = TechniqueCsv.Parse(Header + ",lunge,lunge_stops\n" + Row + ",0,true\n");
        Assert.Contains(r.Errors, e => e.Contains("'lunge_stops' is set but 'lunge' is 0"));
    }

    [Fact]
    public void Huxing_quan_rolls_the_whole_way_into_hit_2_and_the_string_holds_wherever_the_pounce_reaches()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var tiger = book.Find("huxing_quan_1")!;
        var roll = book.Find("huxing_quan_2")!;
        int limit = tiger.StringReachFrom(id => book.Find(id));
        Assert.Equal(tiger.FullReachCm, limit);
        for (int apart = rules.MinDistance + 5; apart <= limit; apart += 5)
        {
            var loadout = new Loadout(book);
            loadout.Assign(Slot.Square, tiger);
            var d = new Duel(rules, loadout, new Loadout(book), 0, apart);
            int landed = 0, rollFrames = 0, still = 0;
            for (int t = 0; t < 150 && landed < 3; t++)
            {
                bool stunned = d.Two.State == FighterState.Hitstun;
                int x = d.One.XCm;
                // Press as each hit is out: kept until its chain window opens, so each next hit chains on the first chain frame.
                bool press = t == 0 || d.One.State == FighterState.Active && d.One.StringHit < 3;
                foreach (var h in d.Step(press ? Square : None, None))
                {
                    if (h.Attacker != Side.One) continue;
                    Assert.True(h.Landed, $"{apart} cm: {h.Technique.Id} didn't land");
                    if (h.Technique != tiger) Assert.True(stunned, $"{apart} cm: the opponent was free before {h.Technique.Id}");
                    landed++;
                }
                if (d.One.Technique == roll && d.One.State == FighterState.Startup)
                {
                    rollFrames++;
                    if (d.One.XCm == x) still++;
                }
            }
            Assert.True(landed == 3, $"{apart} cm: {landed} hits landed");
            Assert.Equal(roll.StartupFrames, rollFrames);
            Assert.True(still <= 1, $"{apart} cm: the roll stood still on {still} of its {rollFrames} frames");
        }
    }

    // ---------- Moving on during the recovery (虎形拳 1's roll when hit 2 doesn't follow) ----------

    /// <summary>Startup 6, active 3, recovery 12, reach 100, carries on <paramref name="cm"/> cm over its recovery; chains into <paramref name="next"/>.</summary>
    private static Technique RollingOn(int cm, string next = "") =>
        new("rolling", "rolling", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 20, 100, next, next.Length > 0 ? 6 : 0,
            recoveryAdvanceCm: cm);

    [Fact]
    public void Recovery_advance_slides_over_the_recovery_frames_only()
    {
        var t = RollingOn(60);
        var d = new Duel(RulesWith(), Holding(t), Holding(Lunge()), 0, 400);
        var steps = new List<int>();
        for (int tick = 0; tick < t.TotalFrames + 3; tick++)
        {
            int x = d.One.XCm;
            d.Step(tick == 0 ? Square : None, None);
            steps.Add(d.One.XCm - x);
        }
        Assert.All(steps.Take(t.StartupFrames + t.ActiveFrames), s => Assert.Equal(0, s)); // still through the wind-up and the hit
        Assert.All(steps.Skip(t.StartupFrames + t.ActiveFrames).Take(t.RecoveryFrames), s => Assert.InRange(s, 5, 5)); // 60 cm / 12 frames
        Assert.Equal(60, d.One.XCm);
    }

    [Fact]
    public void Chaining_the_next_hit_on_the_first_chain_frame_skips_the_recovery_advance()
    {
        var next = new Technique("next", "next", Category.Waigong, Weight.Medium, false, 6, 3, 12, 0, 0, 0, 0, 5, true, 20, 100);
        var first = RollingOn(60, "next");
        var l = new Loadout(new TechniqueBook(new[] { first, next }));
        l.Assign(Slot.Square, first);
        var d = new Duel(RulesWith(), l, Holding(Lunge()), 0, 400);
        // Pressed during the hit, the press is kept and chains on the first chain frame: the recovery never plays.
        for (int tick = 0; tick < first.StartupFrames + first.ActiveFrames + next.StartupFrames; tick++)
            d.Step(tick == 0 || d.One.State == FighterState.Active && d.One.StringHit < 2 ? Square : None, None);
        Assert.Equal(2, d.One.StringHit);
        Assert.Equal(next.StartupFrames - 1, d.One.MoveFrame); // chained on the very first chain frame
        Assert.Equal(0, d.One.XCm);
    }

    [Fact]
    public void Recovery_advance_stops_at_min_distance()
    {
        var d = new Duel(RulesWith(), Holding(RollingOn(200)), Holding(Lunge()), 0, 150);
        for (int tick = 0; tick < 30; tick++) d.Step(tick == 0 ? Square : None, None);
        Assert.Equal(80, d.DistanceCm);
    }

    [Theory]
    [InlineData("301", "'recovery_advance' (301) must be 300 cm or less")]
    [InlineData("-5", "'recovery_advance' must be a whole number 0 or more")]
    public void Bad_recovery_advance_is_reported(string value, string expected)
    {
        var r = TechniqueCsv.Parse(Header + ",recovery_advance\n" + Row + "," + value + "\n");
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Recovery_advance_is_optional_and_read_from_the_sheet()
    {
        Assert.Equal(0, TechniqueCsv.Parse(Header + "\n" + Row + "\n").Techniques[0].RecoveryAdvanceCm);
        var r = TechniqueCsv.Parse(Header + ",recovery_advance\n" + Row + ",40\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(40, r.Techniques[0].RecoveryAdvanceCm);
    }

    [Fact]
    public void Huxing_quan_1_alone_rolls_forward_through_its_whole_recovery()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var tiger = book.Find("huxing_quan_1")!;
        Assert.True(tiger.RecoveryAdvanceCm > 0);
        for (int apart = rules.MinDistance + 5; apart <= tiger.FullReachCm; apart += 5)
        {
            var loadout = new Loadout(book);
            loadout.Assign(Slot.Square, tiger);
            var d = new Duel(rules, loadout, new Loadout(book), 0, apart);
            int recoveryFrames = 0, still = 0;
            for (int t = 0; t < tiger.TotalFrames + 2; t++)
            {
                int x = d.One.XCm;
                d.Step(t == 0 ? Square : None, None);
                if (d.One.State != FighterState.Recovery) continue;
                recoveryFrames++;
                if (d.One.XCm == x) still++;
            }
            Assert.True(Assert.Single(d.Hits).Landed, $"{apart} cm");
            Assert.Equal(tiger.RecoveryFrames, recoveryFrames);
            Assert.True(still <= 1, $"{apart} cm: the roll stood still on {still} of its {recoveryFrames} frames");
            Assert.True(d.DistanceCm >= rules.MinDistance);
        }
    }
}
