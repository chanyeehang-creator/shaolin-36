using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Full 3D: the fighters stand anywhere on a square floor (X and Z), face each other (auto lock-on),
/// walk in any direction relative to that, and a strike must reach across the floor and fall inside its hit angle.
/// </summary>
public class FloorTests
{
    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    // Lands on frame 10 (active 10-12), reach 120 cm, a 60-degree wedge (30 either side).
    private static readonly Technique Jab = Make.Move("jab", startup: 10, active: 3, recovery: 12, window: 0, perfect: 0,
        stamina: 0, damage: 5, hitstun: 20, reach: 120, hitAngle: 60);

    private static Rules RulesWith(params (string key, string value)[] extra)
    {
        var all = new List<(string, string)>
        {
            ("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("walk_speed", "180"), ("min_distance", "80"), ("start_distance", "110"), ("arena_width", "1400"),
            ("knockback_frames", "8"),
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

    private static Duel NewDuel(int twoX = 110, int twoZ = 0, Technique? move = null, Rules? rules = null)
    {
        var d = new Duel(rules ?? RulesWith(), Holding(move ?? Jab), Holding(move ?? Jab), 0, 0);
        d.Two.Position = FloorPoint.FromCm(twoX, twoZ);
        d.Step(None, None); // one tick so both turn to face each other
        return d;
    }

    // ---------- FloorPoint ----------

    [Fact]
    public void A_centimetre_is_60_floor_units_so_cm_per_second_is_units_per_tick()
    {
        Assert.Equal(Sim.TicksPerSecond, FloorPoint.UnitsPerCm);
        var p = FloorPoint.FromCm(3, -4);
        Assert.Equal(180, p.X);
        Assert.Equal(-240, p.Z);
        Assert.Equal(5, FloorPoint.DistanceCm(p, FloorPoint.Zero));
    }

    [Fact]
    public void Right_turn_is_a_quarter_turn_to_the_right()
    {
        // Facing +X (the player at the start), your right hand points to +Z.
        Assert.Equal(new FloorPoint(0, 1), new FloorPoint(1, 0).RightTurn);
        Assert.Equal(new FloorPoint(1, 0), new FloorPoint(0, -1).RightTurn);
    }

    [Fact]
    public void WithLength_scales_a_direction_and_rounds_to_whole_units()
    {
        Assert.Equal(new FloorPoint(30, 40), new FloorPoint(3, 4).WithLength(50));
        Assert.Equal(FloorPoint.Zero, FloorPoint.Zero.WithLength(50));
    }

    // ---------- Facing (lock-on) ----------

    [Fact]
    public void Fighters_start_facing_each_other()
    {
        var d = Duel.Start(RulesWith(), Holding(Jab), Holding(Jab));
        Assert.Equal(-55, d.One.XCm);
        Assert.Equal(0, d.One.ZCm);
        Assert.True(d.One.Facing.X > 0 && d.One.Facing.Z == 0);
        Assert.True(d.Two.Facing.X < 0 && d.Two.Facing.Z == 0);
    }

    [Fact]
    public void An_idle_fighter_keeps_turning_to_face_the_other()
    {
        var d = NewDuel();
        d.Two.Position = FloorPoint.FromCm(0, 100); // walks round to One's right
        d.Step(None, None);
        Assert.Equal(0, d.One.Facing.X);
        Assert.True(d.One.Facing.Z > 0);
        Assert.True(d.Two.Facing.Z < 0);
    }

    [Fact]
    public void A_move_keeps_the_aim_it_started_with()
    {
        var d = NewDuel();
        d.Step(Square, None);
        var aim = d.One.Facing;
        d.Two.Position = FloorPoint.FromCm(0, 100);
        d.Step(None, None);
        Assert.Equal(aim, d.One.Facing);
        Assert.True(d.Two.Facing.Z < 0); // Two isn't in a move, so it turned
    }

    // ---------- Walking in any direction ----------

    [Fact]
    public void Walking_forward_goes_straight_at_the_other_fighter()
    {
        var d = NewDuel(twoX: 0, twoZ: 400);
        for (int i = 0; i < Sim.TicksPerSecond; i++) d.Step(None, None, MoveInput.WalkForward, MoveInput.None);
        Assert.Equal(0, d.One.XCm);
        Assert.Equal(180, d.One.ZCm);
    }

    [Fact]
    public void Walking_right_sidesteps_round_the_other_fighter()
    {
        var d = NewDuel();
        d.Step(None, None, MoveInput.WalkRight, MoveInput.None);
        Assert.Equal(0, d.One.XCm);
        Assert.Equal(3, d.One.ZCm); // 180 cm/s is 3 cm a tick, to the +Z side
        for (int i = 0; i < 29; i++) d.Step(None, None, MoveInput.WalkRight, MoveInput.None);
        // Half a second of strafing: about 90 cm round the side, and still about as far away.
        Assert.InRange(d.One.ZCm, 80, 90);
        Assert.InRange(d.DistanceCm, 110, 115);
        Assert.True(d.One.Facing.Z < 0 || d.One.Facing.X > 0); // still facing Two
    }

    [Fact]
    public void A_diagonal_is_no_faster_than_a_straight_walk()
    {
        var d = NewDuel(twoX: 700);
        for (int i = 0; i < Sim.TicksPerSecond; i++) d.Step(None, None, new MoveInput(100, 100), MoveInput.None);
        double cm = d.One.Position.Length / FloorPoint.UnitsPerCm;
        Assert.InRange(cm, 179, 181);
        Assert.True(d.One.XCm > 0 && d.One.ZCm > 0);
    }

    [Fact]
    public void A_half_pushed_stick_walks_at_half_speed()
    {
        var d = NewDuel(twoX: 700);
        for (int i = 0; i < Sim.TicksPerSecond; i++) d.Step(None, None, new MoveInput(50, 0), MoveInput.None);
        Assert.Equal(90, d.One.XCm);
    }

    [Fact]
    public void Walking_into_the_other_fighter_from_an_angle_stops_at_min_distance()
    {
        var d = NewDuel(twoX: 200, twoZ: 150);
        for (int i = 0; i < 120; i++) d.Step(None, None, MoveInput.WalkForward, MoveInput.None);
        Assert.Equal(80, d.DistanceCm);
        Assert.Equal(200, d.Two.XCm);
        Assert.Equal(150, d.Two.ZCm);
    }

    [Fact]
    public void Circling_close_in_slides_round_without_getting_closer()
    {
        var d = NewDuel(twoX: 80);
        for (int i = 0; i < 60; i++)
        {
            d.Step(None, None, new MoveInput(100, 100), MoveInput.None);
            Assert.True(d.DistanceCm >= 80);
        }
        Assert.True(d.One.ZCm > 30); // got round the side
    }

    [Fact]
    public void The_arena_edges_stop_walking_along_Z_too()
    {
        var d = NewDuel(twoX: 0, twoZ: 100, rules: RulesWith(("arena_width", "300")));
        for (int i = 0; i < 300; i++) d.Step(None, None, MoveInput.WalkBack, MoveInput.None);
        Assert.Equal(-150, d.One.ZCm);
        Assert.Equal(0, d.One.XCm);
    }

    // ---------- Reach across the floor and the hit angle ----------

    [Theory]
    [InlineData(72, 96, true)]  // exactly 120 cm away, on a slant
    [InlineData(73, 97, false)] // just past 120
    public void Reach_is_measured_across_the_floor(int x, int z, bool hits)
    {
        var allRound = Make.Move("all", startup: 10, active: 3, window: 0, perfect: 0, stamina: 0, reach: 120);
        var d = NewDuel(twoX: x, twoZ: z, move: allRound);
        d.Step(Square, None);
        for (int t = 1; t <= 12; t++) d.Step(None, None);
        Assert.Equal(hits ? 1 : 0, d.Hits.Count);
    }

    [Fact]
    public void Hit_angle_covers_a_wedge_round_the_aim()
    {
        var jab = Jab; // 60 degrees: 30 to either side
        var facing = new FloorPoint(1, 0);
        Assert.True(jab.Covers(facing, FloorPoint.FromCm(100, 0)));
        Assert.True(jab.Covers(facing, FloorPoint.FromCm(100, 57)));   // 29.7 degrees off
        Assert.False(jab.Covers(facing, FloorPoint.FromCm(100, 58)));  // 30.1 degrees off
        Assert.False(jab.Covers(facing, FloorPoint.FromCm(-100, 0)));  // behind
        Assert.True(Make.Move("all").Covers(facing, FloorPoint.FromCm(-100, 0))); // 360 hits all round
    }

    [Fact]
    public void A_strike_hits_a_target_inside_its_angle()
    {
        var d = NewDuel();
        d.Step(Square, None);
        for (int t = 1; t <= 10; t++) d.Step(None, None);
        Assert.Single(d.Hits);
    }

    [Fact]
    public void Stepping_to_the_side_during_the_wind_up_slips_the_strike()
    {
        var d = NewDuel(twoX: 90);
        d.Step(Square, None); // aims at (90, 0)
        // Two side-steps during the wind-up: still within 120 cm, but 42 degrees off the aim.
        d.Two.Position = FloorPoint.FromCm(60, 55);
        for (int t = 1; t <= 14; t++) d.Step(None, None);
        Assert.Empty(d.Hits);
        Assert.True(d.DistanceCm <= 120);
    }

    [Fact]
    public void Each_hit_of_a_string_aims_afresh()
    {
        var two = Make.Move("two", startup: 6, active: 3, recovery: 12, window: 0, perfect: 0, stamina: 0, reach: 120, hitAngle: 60);
        var one = Make.Move("one", startup: 10, active: 3, recovery: 12, window: 0, perfect: 0, stamina: 0, reach: 120, hitAngle: 60,
            next: "two", chain: 6, hitstun: 0);
        var l = new Loadout(new TechniqueBook(new[] { one, two }));
        l.Assign(Slot.Square, one);
        var d = new Duel(RulesWith(), l, Holding(Jab), 0, 100);
        d.Step(None, None);
        d.Step(Square, None);
        for (int t = 1; t <= 10; t++) d.Step(None, None);
        Assert.Single(d.Hits);
        d.Two.Position = FloorPoint.FromCm(50, 70); // moved 54 degrees off the first aim
        d.Step(None, None);
        d.Step(None, None);
        d.Step(Square, None); // chains: hit 2 aims at where Two is now
        for (int t = 1; t <= 6; t++) d.Step(None, None);
        Assert.Equal(2, d.Hits.Count);
    }

    // ---------- Knockback on the floor ----------

    [Fact]
    public void Knockback_pushes_straight_away_from_the_attacker_on_a_slant()
    {
        var shove = new Technique("shove", "shove", Category.Waigong, Weight.Medium, false, 5, 3, 12, 0, 0, 0, 0, 6, true, 30, 200,
            knockbackCm: 50);
        var d = new Duel(RulesWith(), Holding(shove), Holding(shove), 0, 0);
        d.Two.Position = FloorPoint.FromCm(60, 80); // 100 cm away
        d.Step(None, None);
        d.Step(Square, None);
        for (int t = 1; t <= 5 + 8; t++) d.Step(None, None);
        Assert.Single(d.Hits);
        Assert.Equal(90, d.Two.XCm);  // 150 cm away along the same line
        Assert.Equal(120, d.Two.ZCm);
    }

    [Fact]
    public void The_same_inputs_always_give_the_same_floor_positions()
    {
        string Run()
        {
            var d = NewDuel(twoX: 150, twoZ: 40);
            var log = new System.Text.StringBuilder();
            for (int i = 0; i < 400; i++)
            {
                var m1 = new MoveInput((i / 13) % 3 * 70 - 70, (i / 7) % 5 * 45 - 90);
                var m2 = new MoveInput((i / 11) % 3 * 60 - 60, (i / 19) % 3 * 100 - 100);
                d.Step(i % 41 == 0 ? Square : None, i % 53 == 0 ? Square : None, m1, m2);
                log.Append(d.One.Position.X).Append(',').Append(d.One.Position.Z).Append(';')
                   .Append(d.Two.Position.X).Append(',').Append(d.Two.Position.Z).Append('|');
            }
            return log.ToString();
        }
        Assert.Equal(Run(), Run());
    }

    // ---------- The sheet ----------

    private const string Header =
        "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry";
    private const string Row = "punch,punch,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0";

    [Fact]
    public void Hit_angle_is_optional_and_defaults_to_all_round()
    {
        var r = TechniqueCsv.Parse(Header + "\n" + Row + "\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(360, r.Techniques[0].HitAngle);

        r = TechniqueCsv.Parse(Header + ",hit_angle\n" + Row + ",70\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(70, r.Techniques[0].HitAngle);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("361")]
    public void A_hit_angle_outside_1_to_360_is_reported(string angle)
    {
        var r = TechniqueCsv.Parse(Header + ",hit_angle\n" + Row + "," + angle + "\n");
        Assert.Contains(r.Errors, e => e.Contains("'hit_angle'") && e.Contains("between 1 and 360"));
    }

    [Fact]
    public void Every_strike_on_the_sheet_has_a_hit_angle_you_can_step_out_of()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv")));
        Assert.True(sheet.Ok, string.Join("\n", sheet.Errors));
        foreach (var t in sheet.Techniques.Where(t => t.ReachCm > 0))
            Assert.InRange(t.HitAngle, 30, 120);
    }
}
