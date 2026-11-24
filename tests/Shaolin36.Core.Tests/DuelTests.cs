using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

public class DuelTests
{
    // Startup 10, active on frames 10-12, recovery 12; reach 120 cm, 7 damage, 18 frames hitstun.
    private static readonly Technique Punch = Make.Move("punch", startup: 10, active: 3, recovery: 12, reach: 120, damage: 7, hitstun: 18);
    private static readonly Technique Parry = Make.ParryMove();

    private static Loadout Holding(Technique t, Slot slot = Slot.Square)
    {
        var l = new Loadout();
        l.Assign(slot, t);
        return l;
    }

    private static Duel Make2(int distanceCm, Technique? one = null, Technique? two = null, Rules? rules = null) =>
        new(rules ?? Make.TestRules(), Holding(one ?? Punch), Holding(two ?? Punch), 0, distanceCm);

    private static readonly SlotInput Press = SlotInput.Press(Slot.Square);
    private static readonly SlotInput None = SlotInput.None;

    /// <summary>Fighter One presses on tick 0, then both wait. Returns every hit in the first <paramref name="ticks"/> ticks.</summary>
    private static IReadOnlyList<HitEvent> OneAttacks(Duel d, int ticks = 60)
    {
        d.Step(Press, None);
        for (int i = 1; i < ticks; i++) d.Step(None, None);
        return d.Hits;
    }

    [Fact]
    public void Both_fighters_start_with_the_rules_sheet_hp()
    {
        var rules = Make.TestRules();
        var d = Make2(100, rules: rules);
        Assert.Equal(rules.HpMax, d.One.Hp);
        Assert.Equal(rules.HpMax, d.Two.Hp);
        Assert.Equal(100, d.DistanceCm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(120)] // exactly at the edge of reach still connects
    public void In_reach_the_hit_lands_on_the_first_active_frame(int distance)
    {
        var d = Make2(distance);
        var hits = OneAttacks(d);

        var hit = Assert.Single(hits);
        Assert.Equal(Punch.StartupFrames, hit.Tick); // the move started on tick 0, so frame 10 is tick 10
        Assert.Equal(Side.One, hit.Attacker);
        Assert.Equal(Side.Two, hit.Defender);
        Assert.Same(Punch, hit.Technique);
        Assert.Equal(7, hit.Damage);
        Assert.Equal(d.Rules.HpMax - 7, d.Two.Hp);
        Assert.Equal(d.Two.Hp, hit.DefenderHpAfter);
        Assert.Equal(d.Rules.HpMax, d.One.Hp);
    }

    [Theory]
    [InlineData(121)]
    [InlineData(300)]
    public void Out_of_reach_the_hit_whiffs(int distance)
    {
        var d = Make2(distance);
        Assert.Empty(OneAttacks(d));
        Assert.Equal(d.Rules.HpMax, d.Two.Hp);
        Assert.Equal(FighterState.Idle, d.Two.State);
    }

    [Fact]
    public void Reach_works_from_either_side_of_the_line()
    {
        var d = new Duel(Make.TestRules(), Holding(Punch), Holding(Punch), 500, 380); // One stands to the right
        Assert.Single(OneAttacks(d));
    }

    [Fact]
    public void A_move_hits_once_even_with_several_active_frames()
    {
        var longActive = Make.Move("long", startup: 5, active: 8, recovery: 10, reach: 120, hitstun: 2);
        var d = Make2(50, longActive);
        Assert.Single(OneAttacks(d));
    }

    [Fact]
    public void Walking_into_reach_during_the_active_frames_gets_you_hit()
    {
        var d = Make2(200);
        d.Step(Press, None);
        for (int t = 1; t <= Punch.StartupFrames; t++) d.Step(None, None); // tick 10: first active frame, out of reach
        Assert.Empty(d.Hits);

        d.Two.PositionCm = 110; // steps in before tick 11, the second active frame
        var hit = Assert.Single(d.Step(None, None));
        Assert.Equal(Punch.StartupFrames + 1, hit.Tick);
    }

    [Fact]
    public void Walking_in_after_the_active_frames_is_safe()
    {
        var d = Make2(200);
        d.Step(Press, None);
        for (int t = 1; t < Punch.StartupFrames + Punch.ActiveFrames; t++) d.Step(None, None);
        d.Two.PositionCm = 50; // first recovery frame is next
        for (int t = 0; t < 30; t++) d.Step(None, None);
        Assert.Empty(d.Hits);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(1)]
    [InlineData(34)]
    public void Hitstun_holds_the_defender_exactly_its_length_from_the_hit_frame(int hitstun)
    {
        var punch = Make.Move("p", startup: 10, active: 3, recovery: 12, reach: 120, hitstun: hitstun);
        var d = Make2(100, punch, punch);
        d.Step(Press, None);
        for (int t = 1; t < punch.StartupFrames; t++) d.Step(None, None);

        Assert.Single(d.Step(None, None)); // tick 10: the hit lands
        Assert.Equal(FighterState.Hitstun, d.Two.State);

        // Ticks 11 .. 10 + hitstun - 1: still frozen, mashing does nothing.
        for (int i = 1; i < hitstun; i++)
        {
            d.Step(None, Press);
            Assert.Equal(FighterState.Hitstun, d.Two.State);
        }

        // Tick 10 + hitstun: free, and the press starts a move on that very tick.
        d.Step(None, Press);
        Assert.Equal(FighterState.Startup, d.Two.State);
        Assert.Equal(0, d.Two.MoveFrame);
    }

    [Fact]
    public void A_hit_with_no_hitstun_deals_damage_but_does_not_freeze()
    {
        var poke = Make.Move("poke", startup: 4, active: 2, recovery: 6, reach: 120, hitstun: 0, damage: 3);
        var d = Make2(100, poke);
        Assert.Single(OneAttacks(d));
        Assert.Equal(d.Rules.HpMax - 3, d.Two.Hp);
        Assert.Equal(FighterState.Idle, d.Two.State);
    }

    [Fact]
    public void A_hit_interrupts_the_defenders_move()
    {
        var slow = Make.Move("slow", startup: 30, active: 3, recovery: 12, reach: 120);
        var d = Make2(100, Punch, slow);
        d.Step(Press, Press); // both start on tick 0; the punch is active on tick 10, the slow move on tick 30
        for (int t = 1; t < 60; t++) d.Step(None, None);

        var hit = Assert.Single(d.Hits);
        Assert.Equal(Side.One, hit.Attacker);
        Assert.Equal(d.Rules.HpMax, d.One.Hp); // the slow move was cancelled before it got active
    }

    [Fact]
    public void Same_tick_hits_trade()
    {
        var d = Make2(100);
        var landed = d.Step(Press, Press);
        for (int t = 1; t <= Punch.StartupFrames; t++) landed = d.Step(None, None);

        Assert.Equal(2, landed.Count);
        Assert.Equal(d.Rules.HpMax - 7, d.One.Hp);
        Assert.Equal(d.Rules.HpMax - 7, d.Two.Hp);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
    }

    [Fact]
    public void Trades_do_not_depend_on_which_side_is_one()
    {
        var shortPunch = Make.Move("short", startup: 10, active: 3, recovery: 12, reach: 80, damage: 4);
        var a = Make2(100, Punch, shortPunch);
        var b = Make2(100, shortPunch, Punch);
        a.Step(Press, Press);
        b.Step(Press, Press);
        for (int t = 1; t < 60; t++) { a.Step(None, None); b.Step(None, None); }

        Assert.Equal(Side.One, Assert.Single(a.Hits).Attacker); // only the 120 cm punch reaches 100 cm
        Assert.Equal(Side.Two, Assert.Single(b.Hits).Attacker);
        Assert.Equal(a.Two.Hp, b.One.Hp);
    }

    [Fact]
    public void A_hit_during_hitstun_restarts_the_hitstun()
    {
        var quick = Make.Move("quick", startup: 2, active: 1, recovery: 2, reach: 120, hitstun: 10, damage: 1); // 5 frames total
        var d = Make2(100, quick);
        // One presses every tick: a new jab starts every 5 ticks, hitting on ticks 2, 7, 12 ...
        int lastHitTick = -1;
        for (int t = 0; t < 40; t++)
        {
            foreach (var h in d.Step(Press, Press)) lastHitTick = h.Tick;
            if (t >= 2) Assert.Equal(FighterState.Hitstun, d.Two.State); // never escapes: 10 frames of stun, a hit every 5
        }
        Assert.Equal(8, d.Hits.Count);
        Assert.Equal(37, lastHitTick);
    }

    [Fact]
    public void A_parry_never_hits()
    {
        var d = Make2(0, Parry);
        Assert.Empty(OneAttacks(d));
    }

    [Fact]
    public void Hp_stops_at_zero_and_the_duel_ends()
    {
        var rules = Make.TestRules();
        var huge = Make.Move("huge", startup: 2, active: 1, recovery: 2, reach: 120, damage: rules.HpMax + 50);
        var d = Make2(100, huge, rules: rules);
        var hits = OneAttacks(d);

        Assert.Single(hits);
        Assert.Equal(0, d.Two.Hp);
        Assert.True(d.Two.IsDown);
        Assert.True(d.IsOver);
        int tick = d.Tick;
        Assert.Empty(d.Step(Press, Press));
        Assert.Equal(tick, d.Tick);
    }

    [Fact]
    public void The_same_inputs_give_the_same_hits_every_time()
    {
        var rng = new Random(36);
        var script = new List<(SlotInput one, SlotInput two, int oneMove, int twoMove)>();
        for (int i = 0; i < 3_000; i++)
        {
            SlotInput Pick() => rng.Next(6) == 0 ? SlotInput.Press(rng.Next(2) == 0 ? Slot.Square : Slot.L1) : SlotInput.None;
            script.Add((Pick(), Pick(), rng.Next(21) - 10, rng.Next(21) - 10));
        }

        List<string> Run()
        {
            var rules = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), ("hp_max", "100000"))).Rules!;
            Loadout L() { var l = Holding(Punch); l.Assign(Slot.L1, Make.Move("far", startup: 14, active: 2, recovery: 20, reach: 180)); return l; }
            var d = new Duel(rules, L(), L(), 0, 150);
            var log = new List<string>();
            foreach (var (one, two, m1, m2) in script)
            {
                d.One.PositionCm += m1;
                d.Two.PositionCm += m2;
                foreach (var h in d.Step(one, two)) log.Add(h.ToString());
                log.Add($"{d.One} | {d.Two}");
            }
            return log;
        }

        var first = Run();
        Assert.Equal(first, Run());
        Assert.Contains(first, s => s.Contains(" hits with "));
    }

    [Fact]
    public void Luohan_quan_1_from_the_sheet_reaches_120_cm_for_6_damage_and_37_frames_hitstun()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques.ToDictionary(t => t.Id);
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var punch = sheet["luohan_quan_1"];
        var parry = sheet["timed_parry"];

        var inReach = new Duel(rules, Holding(punch), Holding(parry), 0, 120);
        var hit = Assert.Single(OneAttacks(inReach));
        Assert.Equal(19, hit.Tick);
        Assert.Equal(6, hit.Damage);
        Assert.Equal(37, hit.HitstunFrames);
        Assert.Equal(114, inReach.Two.Hp);

        Assert.Empty(OneAttacks(new Duel(rules, Holding(punch), Holding(parry), 0, 121)));
        Assert.Empty(OneAttacks(new Duel(rules, Holding(parry), Holding(punch), 0, 0)));
    }

    [Fact]
    public void Fighter_rejects_bad_hits_and_hp()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maxHp: 0));
        var f = new Fighter(maxHp: 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => f.TakeHit(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.TakeHit(1, -1));
        Assert.Throws<InvalidOperationException>(() => f.MarkHitLanded());
    }
}
