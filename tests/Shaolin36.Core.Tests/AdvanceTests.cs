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
    public void The_dummy_steps_in_with_its_string_too()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var dummyLoadout = new Loadout(book);
        dummyLoadout.Assign(Slot.Square, book.Find("luohan_quan_1")!);
        var d = Duel.Start(rules, new Loadout(book), dummyLoadout);
        var dummy = TrainingDummy.FromRules(rules, Slot.Square);
        int start = d.Two.XCm;
        for (int t = 0; t <= rules.DummyPause + 13; t++) d.Step(None, dummy.Think(d.Two));
        Assert.Equal(start - book.Find("luohan_quan_1")!.AdvanceCm, d.Two.XCm); // toward the player at -X
        Assert.Single(d.Hits);
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
        foreach (var t in sheet.Where(t => t.Category == Category.Waigong))
            Assert.InRange(t.AdvanceCm, 5, 30);
    }
}
