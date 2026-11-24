using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Stage 1 step 7: stamina. Every move pays its cost when it starts; regen after a short rest,
/// faster when standing still; at 0 the fighter is exhausted (no moves that cost stamina, so no
/// parry, half-speed walking, extra damage taken) until stamina refills to exhausted_until.
/// A perfect parry refunds the parry's cost and drains the attacker.
/// </summary>
public class StaminaTests
{
    // 100 max, 30/s moving, 60/s still (1 a tick), 10-tick delay, exhausted until 50, +25% damage, half-speed walk.
    private static readonly StaminaRules S = new(100, 30, 60, 10, 50, 125, 50);

    // Punch: startup 10, active 3, recovery 12 (25 frames); costs 8; chains into punch2 (costs 9).
    private static readonly Technique Punch = Make.Move("punch", startup: 10, active: 3, recovery: 12, hitstun: 18,
        reach: 120, damage: 6, next: "punch2", chain: 8, stamina: 8);
    private static readonly Technique Punch2 = Make.Move("punch2", startup: 10, damage: 12, stamina: 9);
    private static readonly Technique Free = Make.Move("free", stamina: 0);
    private static readonly Technique Parry = Make.ParryMove(); // costs 10; window frames 3-12, perfect 3-6; 33 frames if it whiffs
    private static readonly TechniqueBook Book = new(new[] { Punch, Punch2, Free, Parry });

    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);
    private static readonly SlotInput Circle = SlotInput.Press(Slot.Circle);

    private static Loadout Holding(Technique square, Technique? circle = null)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, square);
        if (circle != null) l.Assign(Slot.Circle, circle);
        return l;
    }

    private static Fighter WithStamina(Technique square, Technique? circle = null) =>
        new(Holding(square, circle), 100, 0, 0, S);

    private static void Wait(Fighter f, int ticks, bool walking = false)
    {
        for (int i = 0; i < ticks; i++) f.Tick(None, walking);
    }

    /// <summary>Drain <paramref name="f"/> to exactly <paramref name="left"/> stamina.</summary>
    private static void DrainTo(Fighter f, int left) => f.LoseStamina(f.Stamina - left);

    // ---------- Costs ----------

    [Fact]
    public void A_fighter_starts_with_full_stamina()
    {
        var f = WithStamina(Punch);
        Assert.Equal(100, f.MaxStamina);
        Assert.Equal(100, f.Stamina);
        Assert.False(f.IsExhausted);
    }

    [Fact]
    public void Starting_a_move_pays_its_cost_on_that_tick()
    {
        var f = WithStamina(Punch);
        f.Tick(Square);
        Assert.Same(Punch, f.Technique);
        Assert.Equal(92, f.Stamina);
    }

    [Fact]
    public void Each_hit_of_a_string_pays_its_own_cost()
    {
        var f = WithStamina(Punch);
        f.Tick(Square);
        Wait(f, 12); // to the first recovery frame (frame 13)
        f.Tick(Square);
        Assert.Same(Punch2, f.Technique);
        Assert.Equal(100 - 8 - 9, f.Stamina);
    }

    [Fact]
    public void Without_stamina_rules_moves_are_free()
    {
        var f = new Fighter(Holding(Punch));
        f.Tick(Square);
        Assert.False(f.UsesStamina);
        Assert.Equal(0, f.Stamina);
        Assert.False(f.IsExhausted);
        Assert.Same(Punch, f.Technique);
    }

    // ---------- Running out ----------

    [Fact]
    public void A_move_that_costs_more_than_is_left_still_comes_out_and_exhausts()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 3);
        f.Tick(Square);
        Assert.Same(Punch, f.Technique);
        Assert.Equal(0, f.Stamina);
        Assert.True(f.IsExhausted);
    }

    [Fact]
    public void An_exhausted_fighter_cannot_start_a_move_that_costs_stamina()
    {
        var f = WithStamina(Parry, Free);
        f.LoseStamina(100);
        Assert.True(f.IsExhausted);

        f.Tick(Square); // the parry: guard broken
        Assert.Equal(FighterState.Idle, f.State);
        Assert.False(f.CanAfford(Parry));

        f.Tick(Circle); // a free move still works
        Assert.Same(Free, f.Technique);
    }

    [Fact]
    public void An_exhausted_fighter_cannot_chain()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 8);
        f.Tick(Square); // exactly enough: 0 left, exhausted
        Assert.True(f.IsExhausted);
        Wait(f, 12);
        f.Tick(Square);
        Assert.Same(Punch, f.Technique); // still the first hit's recovery: the string ended
        Assert.Equal(1, f.StringHit);
    }

    [Fact]
    public void Exhaustion_lasts_until_stamina_is_back_to_exhausted_until()
    {
        var f = WithStamina(Punch);
        f.LoseStamina(100);
        f.GainStamina(49);
        Assert.True(f.IsExhausted);
        f.GainStamina(1);
        Assert.False(f.IsExhausted);
        f.Tick(Square);
        Assert.Same(Punch, f.Technique);
    }

    [Fact]
    public void Stamina_never_goes_below_0_or_above_max()
    {
        var f = WithStamina(Punch);
        f.LoseStamina(500);
        Assert.Equal(0, f.Stamina);
        f.GainStamina(500);
        Assert.Equal(100, f.Stamina);
    }

    // ---------- Regen ----------

    [Fact]
    public void Regen_waits_for_the_delay_then_pays_the_still_rate()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 20);
        Wait(f, 10);
        Assert.Equal(20, f.Stamina); // the 10-tick delay
        Wait(f, 1);
        Assert.Equal(21, f.Stamina); // 60/s = 1 a tick
        Wait(f, 59);
        Assert.Equal(80, f.Stamina);
    }

    [Fact]
    public void Walking_regens_at_the_slower_rate()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 20);
        Wait(f, 10, walking: true);
        Wait(f, 60, walking: true);
        Assert.Equal(50, f.Stamina); // 30 a second
    }

    [Fact]
    public void Odd_rates_add_up_exactly_over_a_second()
    {
        var f = new Fighter(Holding(Punch), 100, 0, 0, new StaminaRules(100, 25, 40, 0, 50, 125, 50));
        DrainTo(f, 10);
        Wait(f, 60);
        Assert.Equal(50, f.Stamina);
        Wait(f, 60, walking: true);
        Assert.Equal(75, f.Stamina);
    }

    [Fact]
    public void No_regen_during_a_move_and_the_delay_restarts_after_it()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 50);
        f.Tick(Square); // 42 left; the punch lasts 25 ticks
        Wait(f, 24);
        Assert.Equal(42, f.Stamina);
        Wait(f, 11); // idle again: 10 ticks of delay, then 1
        Assert.Equal(43, f.Stamina);
    }

    [Fact]
    public void Reeling_from_a_hit_regens_at_the_slower_rate()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 20);
        Wait(f, 10);
        f.TakeHit(0, 30);
        Wait(f, 20);
        Assert.Equal(30, f.Stamina); // 20 ticks at 30/s
    }

    [Fact]
    public void Losing_stamina_restarts_the_delay()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 20);
        Wait(f, 15); // 5 ticks of regen
        Assert.Equal(25, f.Stamina);
        f.LoseStamina(5);
        Wait(f, 10);
        Assert.Equal(20, f.Stamina);
    }

    [Fact]
    public void Regen_stops_at_max()
    {
        var f = WithStamina(Punch);
        DrainTo(f, 95);
        Wait(f, 100);
        Assert.Equal(100, f.Stamina);
    }

    [Fact]
    public void Regen_ends_exhaustion()
    {
        var f = WithStamina(Punch);
        f.LoseStamina(100);
        Wait(f, 10 + 49);
        Assert.True(f.IsExhausted);
        Wait(f, 1);
        Assert.Equal(50, f.Stamina);
        Assert.False(f.IsExhausted);
    }

    [Fact]
    public void Exhausted_damage_rounds_half_up()
    {
        var f = WithStamina(Punch);
        Assert.Equal(6, f.DamageTaken(6));
        f.LoseStamina(100);
        Assert.Equal(8, f.DamageTaken(6)); // 7.5
        Assert.Equal(15, f.DamageTaken(12));
    }

    [Fact]
    public void Bad_stamina_rules_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaRules(0, 1, 1, 0, 0, 100, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaRules(100, 1, 1, 0, 101, 100, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaRules(100, 1, 1, 0, 50, 99, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaRules(100, 1, 1, 0, 50, 100, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => WithStamina(Punch).LoseStamina(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WithStamina(Punch).GainStamina(-1));
    }

    // ---------- In a duel ----------

    private static Rules DuelRules(params (string, string)[] extra)
    {
        var all = new List<(string, string)>
        {
            ("stamina_max", "100"), ("stamina_regen", "30"), ("stamina_regen_still", "60"), ("stamina_regen_delay", "10"),
            ("exhausted_until", "50"), ("exhausted_damage_pct", "125"), ("exhausted_walk_pct", "50"),
            ("qi_start", "0"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("parry_recovery", "8"), ("parry_perfect_recovery", "4"), ("parry_perfect_stagger", "30"), ("parry_perfect_drain", "15"),
            ("clash_recoil_frames", "14"), ("clash_recoil_stamina", "5"), ("clash_lose_stagger", "24"), ("clash_lose_stamina", "8"),
            ("walk_speed", "120"), ("min_distance", "80"), ("start_distance", "110"), ("arena_width", "1000"),
        };
        all.AddRange(extra);
        var r = RulesCsv.Parse(Make.RulesText(all.ToArray()));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static readonly Rules R = DuelRules();

    /// <summary>One punches on tick 0 (lands on tick 10); Two taps the parry on <paramref name="parryTick"/>.</summary>
    private static Duel PunchIntoParry(int parryTick)
    {
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        for (int t = 0; t <= 10; t++) d.Step(t == 0 ? Square : None, t == parryTick ? Square : None);
        return d;
    }

    [Fact]
    public void Duel_fighters_use_the_sheet_stamina()
    {
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        Assert.Equal(100, d.One.MaxStamina);
        Assert.Equal(100, d.Two.Stamina);
    }

    [Fact]
    public void A_normal_block_costs_the_parry_tap()
    {
        var d = PunchIntoParry(2); // parry frame 8: a plain block
        Assert.Equal(ParryOutcome.Parried, d.Hits[0].Parry);
        Assert.Equal(90, d.Two.Stamina);
        Assert.Equal(92, d.One.Stamina); // the attacker only paid for the punch
    }

    [Fact]
    public void A_perfect_parry_refunds_the_tap_and_drains_the_attacker()
    {
        var d = PunchIntoParry(5); // parry frame 5: perfect
        Assert.Equal(ParryOutcome.PerfectParry, d.Hits[0].Parry);
        Assert.Equal(100, d.Two.Stamina);
        Assert.Equal(100 - 8 - 15, d.One.Stamina);
    }

    [Fact]
    public void A_missed_parry_still_costs_the_tap()
    {
        var d = PunchIntoParry(9); // still winding up when the punch lands
        Assert.True(d.Hits[0].Landed);
        Assert.Equal(90, d.Two.Stamina);
    }

    [Fact]
    public void An_exhausted_fighter_takes_extra_damage()
    {
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        d.Two.LoseStamina(100);
        for (int t = 0; t <= 10; t++) d.Step(t == 0 ? Square : None, None);

        var hit = Assert.Single(d.Hits);
        Assert.True(hit.DefenderExhausted);
        Assert.Equal(8, hit.Damage); // 6 x 125% = 7.5, rounded up
        Assert.Equal(1000 - 8, d.Two.Hp);
    }

    [Fact]
    public void A_broken_guard_cannot_parry()
    {
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        d.Two.LoseStamina(100);
        for (int t = 0; t <= 10; t++) d.Step(t == 0 ? Square : None, t == 2 ? Square : None);

        Assert.True(d.Hits[0].Landed);
        Assert.Equal(1000 - 8, d.Two.Hp);
    }

    [Fact]
    public void A_same_weight_clash_costs_both_the_recoil_stamina()
    {
        // Two presses on tick 2, so it's on frame 8 of its punch (clash window, past the perfect frames) when One's lands.
        var d = new Duel(R, Holding(Punch), Holding(Punch), 0, 100);
        for (int t = 0; t <= 10; t++) d.Step(t == 0 ? Square : None, t == 2 ? Square : None);

        Assert.Equal(ClashOutcome.BothRecoil, d.Hits[0].Clash);
        Assert.Equal(100 - 8 - 5, d.One.Stamina);
        Assert.Equal(100 - 8 - 5, d.Two.Stamina);
    }

    [Fact]
    public void An_exhausted_fighter_walks_at_half_speed()
    {
        var d = new Duel(R, Holding(Punch), Holding(Punch), 0, 500);
        d.Step(None, None, 1, 0);
        Assert.Equal(2, d.One.PositionCm); // 120 cm/s = 2 a tick

        d.One.LoseStamina(100);
        d.Step(None, None, 1, 0);
        Assert.Equal(3, d.One.PositionCm); // 60 cm/s = 1 a tick
    }

    [Fact]
    public void The_dummy_waits_while_exhausted_and_attacks_once_it_recovers()
    {
        var d = new Duel(R, Holding(Free), Holding(Punch), 0, 300);
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 0, chainFrame: 99);
        d.Two.LoseStamina(100);

        int startedOn = -1;
        for (int t = 0; t < 200 && startedOn < 0; t++)
        {
            d.Step(None, dummy.Think(d.Two));
            if (d.Two.Technique != null) startedOn = t;
        }

        // 10 ticks of delay, then 1 a tick standing still: 50 on tick 59, so it can afford the punch on tick 60.
        Assert.Equal(60, startedOn);
        Assert.Equal(50 - 8, d.Two.Stamina); // no regen on a tick spent in a move
    }

    // ---------- The real sheets ----------

    [Fact]
    public void On_the_sheet_ten_whiffed_parry_taps_in_a_row_break_the_guard()
    {
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var techniques = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var book = new TechniqueBook(techniques);
        var parry = book.Find("timed_parry")!;
        var l = new Loadout(book);
        l.Assign(Slot.L1, parry);
        var d = new Duel(rules, l, new Loadout(book), -55, 55);

        // Tap as soon as each whiffed parry ends: no rest, so no regen in between.
        int taps = 0;
        for (int t = 0; t < 2000 && taps < 10; t++)
        {
            bool tap = d.One.CanAct;
            d.Step(tap ? SlotInput.Press(Slot.L1) : None, None);
            if (tap) taps++;
        }

        Assert.Equal(rules.StaminaMax - 10 * parry.StaminaCost, d.One.Stamina);
        Assert.True(d.One.IsExhausted);
        Assert.False(d.One.CanAfford(parry));
    }
}
