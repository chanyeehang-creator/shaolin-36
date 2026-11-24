using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Stage 1 step 6: clash and parry inside a real duel. One scenario per row of the clash
/// table (including the brief's finisher example on the real sheet), and parry, perfect parry,
/// missed parries being punished, and a parried hit unable to chain.
///
/// In every scenario fighter One attacks first, pressing on tick 0, so its hit lands on tick
/// 10 (its startup). Fighter Two presses on a later tick, which sets Two's frame on tick 10.
/// </summary>
public class ClashParryDuelTests
{
    // Startup 10 (clash window frames 6-9, perfect 6-7), active 10-12, recovery 12; 18 hitstun.
    private static readonly Technique Punch = Make.Move("punch", startup: 10, active: 3, recovery: 12, window: 4, perfect: 2,
        hitstun: 18, reach: 120, damage: 7, next: "punch2", chain: 8);
    private static readonly Technique Punch2 = Make.Move("punch2", startup: 10, damage: 7);
    private static readonly Technique Heavy = Make.Move("heavy", Weight.Heavy, startup: 10, damage: 12, hitstun: 18);
    private static readonly Technique Light = Make.Move("light", Weight.Light, startup: 10, damage: 4);
    private static readonly Technique Parry = Make.ParryMove(); // window frames 3-12, perfect 3-6
    private static readonly TechniqueBook Book = new(new[] { Punch, Punch2, Heavy, Light, Parry });

    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);
    private static readonly SlotInput Circle = SlotInput.Press(Slot.Circle);

    private static readonly Rules R = Rules();

    private static Rules Rules()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"),
            ("hp_max", "1000"), ("parry_recovery", "8"), ("parry_perfect_recovery", "4"), ("parry_perfect_stagger", "30"),
            ("clash_recoil_frames", "14"), ("clash_lose_stagger", "24"), ("clash_perfect_stagger", "40"),
            ("clash_perfect_recovery", "6")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    /// <summary>Square holds <paramref name="square"/>; Circle holds a Punch for punishing.</summary>
    private static Loadout Holding(Technique square)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, square);
        l.Assign(Slot.Circle, Punch);
        return l;
    }

    /// <summary>One presses Square on tick 0, Two presses Square on <paramref name="twoPressTick"/>; runs to tick <paramref name="untilTick"/> inclusive.</summary>
    private static Duel Run(Technique one, Technique two, int twoPressTick, int untilTick = 10)
    {
        var d = new Duel(R, Holding(one), Holding(two), 0, 100);
        for (int t = 0; t <= untilTick; t++)
            d.Step(t == 0 ? Square : None, t == twoPressTick ? Square : None);
        return d;
    }

    private static void StepTo(Duel d, int tick, SlotInput? one = null, SlotInput? two = null)
    {
        bool first = true;
        while (d.Tick <= tick)
        {
            d.Step(first ? one ?? None : None, first ? two ?? None : None);
            first = false;
        }
    }

    // ---------- 化绵掌 parry ----------

    [Fact]
    public void A_parry_in_its_window_deflects_the_hit()
    {
        var d = Run(Punch, Parry, twoPressTick: 2); // parry frame 8 on tick 10: plain parry

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.Parried, hit.Parry);
        Assert.False(hit.Landed);
        Assert.Equal(0, hit.Damage);
        Assert.Equal(R.HpMax, d.Two.Hp);

        // The defender recovers for parry_recovery frames from the parry tick...
        Assert.Equal(FighterState.Recoil, d.Two.State);
        Assert.Equal(8, d.Two.StunFramesLeft);
        // ...while the attacker just carries on with its move.
        Assert.Same(Punch, d.One.Technique);
        Assert.True(d.One.ChainBlocked);

        StepTo(d, 17);
        Assert.Equal(FighterState.Recoil, d.Two.State);
        StepTo(d, 18);
        Assert.True(d.Two.CanAct); // 8 frames after the parry
        Assert.False(d.One.CanAct); // the attacker is still recovering until tick 25
    }

    [Fact]
    public void A_parried_hit_cannot_chain_into_the_next_hit()
    {
        var d = Run(Punch, Parry, twoPressTick: 2);
        Assert.Equal(ParryOutcome.Parried, d.Hits[0].Parry);

        // One mashes Square all through recovery, which would normally chain into punch2.
        for (int t = 11; t < 25; t++)
        {
            d.Step(Square, None);
            Assert.False(d.One.InChainWindow);
            Assert.Same(Punch, d.One.Technique);
            Assert.Equal(1, d.One.StringHit);
        }
        StepTo(d, 60);
        Assert.DoesNotContain(d.Hits, h => h.Technique == Punch2);
    }

    [Fact]
    public void An_unparried_hit_still_chains()
    {
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        d.Step(Square, None);
        for (int t = 1; t < 14; t++) d.Step(t == 13 ? Square : None, None);
        Assert.Same(Punch2, d.One.Technique);
    }

    [Fact]
    public void A_perfect_parry_staggers_the_attacker_and_opens_them_for_a_punish()
    {
        var d = Run(Punch, Parry, twoPressTick: 5); // parry frame 5 on tick 10: perfect

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(0, hit.Damage);
        Assert.Equal(FighterState.Stagger, d.One.State);
        Assert.Equal(30, d.One.StunFramesLeft);
        Assert.Null(d.One.Technique); // the attack is stopped
        Assert.Equal(FighterState.Recoil, d.Two.State);
        Assert.Equal(4, d.Two.StunFramesLeft);
        Assert.Equal(R.ParryPerfectQi, hit.ParryEffect!.DefenderQiGain);
        Assert.Equal(Parry.StaminaCost, hit.ParryEffect.DefenderStaminaRefund);
        Assert.Equal(R.ParryPerfectDrain, hit.ParryEffect.AttackerStaminaLoss);

        // Two can act on tick 14 and punches; it lands on tick 24, well before One's stagger ends on tick 40.
        StepTo(d, 14, two: Circle);
        Assert.Same(Punch, d.Two.Technique);
        StepTo(d, 24);
        var punish = d.Hits[1];
        Assert.Equal(Side.Two, punish.Attacker);
        Assert.True(punish.Landed);
        Assert.Equal(24, punish.Tick);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Theory]
    [InlineData(5, ParryOutcome.PerfectParry)] // frame 5: perfect (frames 3-6)
    [InlineData(4, ParryOutcome.PerfectParry)] // frame 6: last perfect frame
    [InlineData(3, ParryOutcome.Parried)]      // frame 7: first plain frame
    [InlineData(0, ParryOutcome.Parried)]      // frame 10
    [InlineData(8, ParryOutcome.NotParried)]   // frame 2: still winding up, too late
    public void The_parry_frame_on_contact_decides_the_outcome(int parryTick, ParryOutcome expected)
    {
        var d = Run(Punch, Parry, parryTick);
        Assert.Equal(expected, Assert.Single(d.Hits).Parry);
    }

    [Fact]
    public void A_parry_pressed_too_late_is_hit_through()
    {
        var d = Run(Punch, Parry, twoPressTick: 8); // parry frame 2: still starting up

        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.Equal(7, hit.Damage);
        Assert.Equal(R.HpMax - 7, d.Two.Hp);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        Assert.Null(d.Two.Technique); // the parry is knocked out
    }

    [Fact]
    public void A_parry_pressed_too_early_is_punished_during_its_recovery()
    {
        // Two parries on tick 0; its window (frames 3-12) is over by tick 13 and it recovers until tick 33.
        // One waits, then punches into the recovery: pressed on tick 10, it lands on tick 20 (parry frame 20).
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        for (int t = 0; t <= 20; t++) d.Step(t == 10 ? Square : None, t == 0 ? Square : None);

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.NotParried, hit.Parry);
        Assert.True(hit.Landed);
        Assert.Same(Parry, hit.DefenderTechnique);
        Assert.Equal(20, hit.DefenderMoveFrame);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
    }

    [Fact]
    public void Holding_does_nothing_so_blocking_a_string_takes_a_fresh_tap_each_time()
    {
        // The parry window is 10 frames and then it recovers: one tap can't cover two hits far apart.
        var d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        for (int t = 0; t <= 40; t++)
            d.Step(t == 0 || t == 25 ? Square : None, t == 2 ? Square : None);
        Assert.Equal(2, d.Hits.Count);
        Assert.Equal(ParryOutcome.Parried, d.Hits[0].Parry);
        Assert.True(d.Hits[1].Landed); // the second punch (tick 35) meets no parry

        // Tapping again in time blocks the second one too.
        d = new Duel(R, Holding(Punch), Holding(Parry), 0, 100);
        for (int t = 0; t <= 40; t++)
            d.Step(t == 0 || t == 25 ? Square : None, t == 2 || t == 27 ? Square : None);
        Assert.All(d.Hits, h => Assert.Equal(ParryOutcome.Parried, h.Parry));
        Assert.Equal(R.HpMax, d.Two.Hp);
    }

    // ---------- Clash table, one row each ----------

    [Fact]
    public void Clash_same_weight_both_recoil_and_nobody_is_hurt()
    {
        var d = Run(Punch, Punch, twoPressTick: 1); // Two's frame 9: in its window, not perfect

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.BothRecoil, hit.Clash);
        Assert.False(hit.Landed);
        Assert.Equal(R.HpMax, d.One.Hp);
        Assert.Equal(R.HpMax, d.Two.Hp);
        Assert.Equal(FighterState.Recoil, d.One.State);
        Assert.Equal(FighterState.Recoil, d.Two.State);
        Assert.Equal(14, d.One.StunFramesLeft);
        Assert.Equal(14, d.Two.StunFramesLeft);
    }

    [Fact]
    public void Clash_heavier_striker_breaks_through_for_full_damage_and_a_stagger()
    {
        var d = Run(Heavy, Punch, twoPressTick: 1);

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.StrikerWins, hit.Clash);
        Assert.True(hit.Landed);
        Assert.Equal(12, hit.Damage);
        Assert.Equal(R.HpMax - 12, d.Two.Hp);
        Assert.Equal(FighterState.Stagger, d.Two.State);
        Assert.Equal(24, d.Two.StunFramesLeft); // the longer of 18 hitstun and 24 stagger
        Assert.Same(Heavy, d.One.Technique);     // the striker's move carries on
    }

    [Fact]
    public void Clash_lighter_striker_is_interrupted_and_the_heavier_move_hits_them()
    {
        var d = Run(Light, Punch, twoPressTick: 1, untilTick: 11);

        Assert.Equal(2, d.Hits.Count);
        var clash = d.Hits[0];
        Assert.Equal(ClashOutcome.TargetWins, clash.Clash);
        Assert.False(clash.Landed);
        Assert.Equal(R.HpMax, d.Two.Hp);

        // Two's punch carried on and lands a tick later on the staggered One.
        var counter = d.Hits[1];
        Assert.Equal(Side.Two, counter.Attacker);
        Assert.Equal(11, counter.Tick);
        Assert.True(counter.Landed);
        Assert.Equal(R.HpMax - 7, d.One.Hp);
    }

    [Fact]
    public void Clash_perfect_breaks_the_target_whatever_the_weights()
    {
        var d = Run(Light, Heavy, twoPressTick: 3); // Two's frame 7: the perfect part of its window

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.PerfectClash, hit.Clash);
        Assert.Equal(0, hit.Damage);
        Assert.Equal(FighterState.Recoil, d.One.State);
        Assert.Equal(6, d.One.StunFramesLeft);
        Assert.Equal(FighterState.Stagger, d.Two.State);
        Assert.Equal(40, d.Two.StunFramesLeft);
        Assert.Null(d.Two.Technique);
        Assert.Equal(R.ClashPerfectQi, hit.ClashEffect!.Striker.QiGain);
    }

    [Fact]
    public void Hitting_before_the_clash_window_is_a_normal_hit_that_interrupts()
    {
        var d = Run(Punch, Heavy, twoPressTick: 5); // Two's frame 5: before its window (6-9)

        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.NoClash, hit.Clash);
        Assert.True(hit.Landed);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        Assert.Null(d.Two.Technique);
    }

    // ---------- The brief's finisher example, on the real sheet ----------

    /// <summary>
    /// The opponent starts the 罗汉拳 finisher; you start hit 1 some frames later. Hitting the
    /// first frames of its clash window is a perfect clash; the rest of the window is a normal
    /// clash you lose because the finisher is heavy; earlier is a plain hit. Worked out from
    /// the sheet so it holds after tuning.
    /// </summary>
    [Fact]
    public void Finisher_example_from_the_sheet()
    {
        var techniques = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(techniques);
        var hit1 = book.Find("luohan_quan_1")!;
        var finisher = book.Find("luohan_quan_4")!;

        ClashOutcome Contact(int delay)
        {
            var you = new Loadout(book);
            you.Assign(Slot.Square, hit1);
            var them = new Loadout(book);
            them.Assign(Slot.Square, finisher);
            var d = new Duel(rules, you, them, 0, Math.Min(hit1.ReachCm, finisher.ReachCm));
            for (int t = 0; d.Hits.Count == 0 && t < 120; t++)
                d.Step(t == delay ? Square : None, t == 0 ? Square : None);
            var first = d.Hits[0];
            Assert.Equal(Side.One, first.Attacker);
            Assert.Equal(delay + hit1.StartupFrames, first.DefenderMoveFrame);
            return first.Clash;
        }

        int windowDelay = finisher.ClashWindowStart - hit1.StartupFrames; // your hit lands on their window's first frame
        Assert.True(windowDelay >= 1, "the example needs hit 1 to be faster than the finisher's wind-up");

        for (int i = 0; i < finisher.PerfectClashFrames; i++)
            Assert.Equal(ClashOutcome.PerfectClash, Contact(windowDelay + i));
        for (int i = finisher.PerfectClashFrames; i < finisher.ClashWindowFrames; i++)
            Assert.Equal(ClashOutcome.TargetWins, Contact(windowDelay + i));
        Assert.Equal(ClashOutcome.NoClash, Contact(windowDelay - 1));
    }

    // ---------- The real sheet's parry against the dummy's string ----------

    [Fact]
    public void Parrying_the_dummys_first_hit_stops_the_whole_string()
    {
        var techniques = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(techniques);
        var parry = book.Find("timed_parry")!;
        var hit1 = book.Find("luohan_quan_1")!;

        var you = new Loadout(book);
        you.Assign(Slot.L1, parry);
        var them = new Loadout(book);
        them.Assign(Slot.Square, hit1);
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 0, chainFrame: 0);
        var d = Duel.Start(rules, you, them);

        // The dummy presses on tick 0, so hit 1 lands on tick 19: parry so it lands on the last window frame.
        int parryTick = hit1.StartupFrames - (parry.StartupFrames + parry.ActiveFrames - 1);
        for (int t = 0; t < hit1.TotalFrames + 1; t++)
        {
            var mine = t == parryTick ? SlotInput.Press(Slot.L1) : None;
            d.Step(mine, dummy.Think(d.Two));
        }

        var only = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.Parried, only.Parry);
        Assert.Equal(rules.HpMax, d.One.Hp);
    }
}
