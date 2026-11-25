using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// The opponent brain (Stage 3 step 1), played on the real technique and rules sheets: it closes in, attacks,
/// chains, parries and dodges what it sees coming, punishes openings and rests when out of breath.
/// </summary>
public class BrainTests
{
    /// <summary>The real rules sheet, with the start-of-duel wait (opponent_ready) as in the game.</summary>
    private static readonly Rules SheetRules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;

    /// <summary>The real rules sheet with no start-of-duel wait, so a test's brain acts from tick 0.</summary>
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    /// <summary>A style that does nothing unless a test turns a knob up.</summary>
    private static BrainStyle Style(int reaction = 0, int thinkEvery = 10, int aggression = 0, int punish = 0, int chain = 0,
        int parry = 0, int perfect = 0, int step = 0, int circle = 0, int restBelow = 0, int heavy = 0, int guess = 0) =>
        new("test", "test", reaction, thinkEvery, aggression, punish, chain, parry, perfect, step, circle, restBelow, heavy, guess);

    /// <summary>The playtest loadout: 罗汉拳 on □, 铁砂掌 on △, 化绵掌 on L1.</summary>
    private static Loadout Starter()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.Triangle, Book.Find("tiesha_zhang")!);
        l.Assign(Slot.L1, Book.Find("timed_parry")!);
        return l;
    }

    /// <summary>You (side One) at 0, the brain (side Two) <paramref name="apart"/> cm along the floor.</summary>
    private static Duel DuelAt(int apart, int? brainQi = null) =>
        new(Rules, Starter(), Starter(), 0, apart, null, brainQi);

    /// <summary>Run <paramref name="ticks"/> ticks: the brain plays Two, <paramref name="you"/> gives One's buttons each tick.</summary>
    private static void Run(Duel duel, Brain brain, int ticks, Func<int, SlotInput>? you = null, Func<int, MoveInput>? youMove = null,
        Action<int>? each = null)
    {
        for (int i = 0; i < ticks && !duel.IsOver; i++)
        {
            var b = brain.Think(duel, Side.Two);
            duel.Step(you?.Invoke(i) ?? SlotInput.None, b.Slots, youMove?.Invoke(i) ?? MoveInput.None, b.Move);
            each?.Invoke(i);
        }
    }

    private static Func<int, SlotInput> PressOnce(Slot slot, int atTick = 0) => i => i == atTick ? SlotInput.Press(slot) : SlotInput.None;

    // ---------- Closing in and attacking ----------

    [Fact]
    public void Engage_distance_is_the_shortest_full_reach_less_the_margin()
    {
        var d = DuelAt(400);
        // 罗汉拳 hit 1: 125 reach + 10 step-in; 铁砂掌: 130 + 20. The parry has no reach.
        Assert.Equal(135 - Brain.EngageMarginCm, Brain.EngageDistance(d.Two));
    }

    [Fact]
    public void It_walks_in_until_its_attacks_reach()
    {
        var d = DuelAt(600);
        var brain = new Brain(Style(), 1);
        Run(d, brain, 60 * 3);
        Assert.InRange(d.DistanceCm, Rules.MinDistance, Brain.EngageDistance(d.Two));
        Assert.Equal(BrainIntent.Waiting, brain.Intent);
    }

    [Fact]
    public void It_quick_steps_in_to_close_a_long_gap_when_step_is_up()
    {
        var d = DuelAt(600);
        var brain = new Brain(Style(step: 100), 1);
        bool stepped = false;
        Run(d, brain, 30, each: _ => stepped |= d.Two.State == FighterState.QuickStep);
        Assert.True(stepped);
    }

    [Fact]
    public void It_never_attacks_from_out_of_reach()
    {
        var d = DuelAt(600);
        var brain = new Brain(Style(aggression: 100, chain: 100), 1);
        int firstAttackDistance = -1;
        Run(d, brain, 60 * 3, each: _ =>
        {
            if (firstAttackDistance < 0 && d.Two.State.IsInMove()) firstAttackDistance = d.DistanceCm;
        });
        Assert.InRange(firstAttackDistance, 1, Brain.EngageDistance(d.Two));
        Assert.NotEmpty(d.Hits);
        Assert.All(d.Hits, h => Assert.Equal(Side.Two, h.Attacker));
    }

    [Fact]
    public void In_reach_with_aggression_100_it_throws_the_whole_string_when_chain_is_100()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(aggression: 100, chain: 100), 1);
        Run(d, brain, 90);
        Assert.Equal(new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" },
            d.Hits.Take(4).Select(h => h.Technique.Id));
        Assert.All(d.Hits.Take(4), h => Assert.True(h.Attacker == Side.Two && h.Landed));
    }

    [Fact]
    public void With_chain_0_it_stops_after_one_hit()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(aggression: 100, thinkEvery: 120), 1);
        Run(d, brain, 60);
        Assert.Single(d.Hits);
        Assert.Equal("luohan_quan_1", d.Hits[0].Technique.Id);
    }

    [Fact]
    public void Heavy_100_picks_the_iron_palm()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(aggression: 100, heavy: 100), 1);
        Run(d, brain, 2);
        Assert.Equal("tiesha_zhang", d.Two.Technique?.Id);
    }

    [Fact]
    public void With_enough_Qi_it_uses_the_empowered_move_whatever_heavy_says()
    {
        var d = DuelAt(110, brainQi: 30);
        var brain = new Brain(Style(aggression: 100, heavy: 0), 1);
        Run(d, brain, 2);
        Assert.Equal("tiesha_zhang", d.Two.Technique?.Id);
        Assert.True(d.Two.Empowered);
    }

    [Fact]
    public void Aggression_0_and_circle_100_circles_without_attacking()
    {
        var d = DuelAt(110);
        var start = d.Two.Position;
        var brain = new Brain(Style(circle: 100), 1);
        Run(d, brain, 60);
        Assert.Empty(d.Hits);
        Assert.Equal(BrainIntent.Circling, brain.Intent);
        Assert.NotEqual(start, d.Two.Position);
    }

    // ---------- Defending ----------

    [Fact]
    public void Parry_100_perfect_100_perfect_parries_your_punch()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 100), 1);
        Run(d, brain, 30, PressOnce(Slot.Square));
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
    }

    [Fact]
    public void Parry_100_perfect_0_blocks_with_the_plain_part_of_the_window()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 0), 1);
        Run(d, brain, 30, PressOnce(Slot.Square));
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.Parried, hit.Parry);
    }

    [Fact]
    public void It_blocks_a_whole_mashed_string_now_the_parry_recovers_in_5_and_opens_in_2()
    {
        // Chan's balance patch: after a block it recovers for parry_recovery (5) frames, a new parry opens in 2, and
        // the window starts with 3 block frames. 罗汉拳 hit 3 lands 9 frames after hit 2, so a re-press straight after
        // a block is in time to block it (it was 8 + 3 = 11 frames before: hit 3 always got through).
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 0), 1);
        Run(d, brain, 120, _ => SlotInput.Press(Slot.Square));
        Assert.Equal(new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" }, d.Hits.Take(4).Select(h => h.Technique.Id));
        Assert.All(d.Hits.Take(4), h => Assert.NotEqual(ParryOutcome.NotParried, h.Parry));
    }

    [Fact]
    public void Too_slow_to_react_and_your_punch_lands()
    {
        // 罗汉拳 hit 1 lands 13 frames after you press: a 20-frame reaction never sees it in time.
        var d = DuelAt(110);
        var brain = new Brain(Style(reaction: 20, parry: 100, perfect: 100), 1);
        Run(d, brain, 30, PressOnce(Slot.Square));
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
    }

    [Fact]
    public void A_slow_strike_is_seen_in_time_even_with_a_slow_reaction()
    {
        // 铁砂掌 winds up for 36 frames: a 20-frame reaction still parries it.
        var d = DuelAt(110);
        var brain = new Brain(Style(reaction: 20, parry: 100, perfect: 100), 1);
        Run(d, brain, 60, PressOnce(Slot.Triangle));
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
    }

    [Fact]
    public void It_does_not_parry_a_strike_that_cannot_reach_it()
    {
        var d = DuelAt(400);
        var brain = new Brain(Style(parry: 100), 1);
        Run(d, brain, 30, PressOnce(Slot.Square));
        Assert.NotEqual(BrainIntent.Parrying, brain.Intent);
        Assert.False(d.Two.State.IsInMove());
    }

    [Fact]
    public void Step_100_slips_your_punch_with_a_side_quick_step()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(step: 100), 1);
        bool stepped = false;
        Run(d, brain, 40, PressOnce(Slot.Square), each: _ => stepped |= d.Two.State == FighterState.QuickStep);
        Assert.True(stepped);
        Assert.Empty(d.Hits);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
    }

    [Fact]
    public void Guess_100_throws_its_parry_while_you_stand_in_reach()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(guess: 100), 1);
        Run(d, brain, 2);
        Assert.Equal("timed_parry", d.Two.Technique?.Id);
        Assert.Equal(BrainIntent.Guessing, brain.Intent);
    }

    [Fact]
    public void A_wrong_guess_can_be_punished()
    {
        // It guesses on its first choice; you wait for the parry to whiff, then hit it in its cooldown.
        var d = DuelAt(110);
        var brain = new Brain(Style(guess: 100, thinkEvery: 120), 1);
        Run(d, brain, 40, PressOnce(Slot.Square, atTick: 16));
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.Equal(Side.One, hit.Attacker);
    }

    [Fact]
    public void It_never_guesses_out_of_your_reach()
    {
        var d = DuelAt(400);
        var brain = new Brain(Style(guess: 100), 1) { Active = true };
        // Hold it far away: walk back as fast as it walks in.
        Run(d, brain, 20, youMove: _ => MoveInput.WalkBack);
        Assert.False(d.Two.State.IsInMove());
    }

    // ---------- Punishing and resting ----------

    [Fact]
    public void It_punishes_you_after_a_perfect_parry_staggers_you()
    {
        // It never attacks on its own here (aggression 0): the only reason to strike is the opening.
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 100, punish: 100), 1);
        Run(d, brain, 60, PressOnce(Slot.Square));
        Assert.Equal(ParryOutcome.PerfectParry, d.Hits[0].Parry);
        Assert.Contains(d.Hits, h => h.Attacker == Side.Two && h.Landed);
    }

    [Fact]
    public void It_punishes_your_recovery_after_blocking_a_single_punch()
    {
        // Your lone 罗汉拳 hit 1 recovers for 21 frames; its block recovers in 8.
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 0, punish: 100), 1);
        Run(d, brain, 60, PressOnce(Slot.Square));
        Assert.Equal(ParryOutcome.Parried, d.Hits[0].Parry);
        Assert.Contains(d.Hits, h => h.Attacker == Side.Two && h.Landed);
    }

    [Fact]
    public void Punish_0_lets_the_opening_go()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(parry: 100, perfect: 100, punish: 0), 1);
        Run(d, brain, 60, PressOnce(Slot.Square));
        Assert.DoesNotContain(d.Hits, h => h.Attacker == Side.Two);
    }

    [Fact]
    public void Low_on_stamina_it_backs_off_to_breathe_instead_of_attacking()
    {
        var d = DuelAt(110);
        d.Two.LoseStamina(Rules.StaminaMax - 10); // 10 left, under rest_below 25
        var brain = new Brain(Style(aggression: 100, restBelow: 25), 1);
        Run(d, brain, 40);
        Assert.Equal(BrainIntent.Resting, brain.Intent);
        Assert.Empty(d.Hits);
        Assert.True(d.DistanceCm > 110);
    }

    [Fact]
    public void Switched_off_it_presses_nothing()
    {
        var d = DuelAt(600);
        var brain = new Brain(Style(aggression: 100), 1) { Active = false };
        var start = d.Two.Position;
        Run(d, brain, 120);
        Assert.Equal(start, d.Two.Position);
        Assert.Equal(BrainIntent.Still, brain.Intent);
    }

    [Fact]
    public void It_waits_opponent_ready_ticks_before_doing_anything()
    {
        var d = Duel.Start(SheetRules, Starter(), Starter());
        var start = d.Two.Position;
        var brain = new Brain(Style(aggression: 100), 1);
        Assert.True(SheetRules.OpponentReady > 0);
        Run(d, brain, SheetRules.OpponentReady);
        Assert.Equal(start, d.Two.Position);
        Assert.Equal(BrainIntent.Still, brain.Intent);
        Run(d, brain, 1);
        Assert.Equal(BrainIntent.Closing, brain.Intent);
    }

    [Fact]
    public void A_new_duel_never_opens_with_a_hit_on_you()
    {
        // Even the sharpest, most aggressive brain needs time to walk in from start_distance.
        var d = Duel.Start(SheetRules, Starter(), Starter());
        var brain = new Brain(Style(aggression: 100, step: 100, thinkEvery: 1), 1);
        int firstHit = -1;
        Run(d, brain, 300, each: i => { if (firstHit < 0 && d.Hits.Count > 0) firstHit = i; });
        Assert.True(firstHit >= SheetRules.OpponentReady + 20, $"first hit on tick {firstHit}");
    }

    // ---------- Determinism and the sheet's brains ----------

    private static List<string> BrainVsBrain(BrainStyle style, int seedOne, int seedTwo, int seconds)
    {
        var d = Duel.Start(Rules, Starter(), Starter());
        var one = new Brain(style, seedOne);
        var two = new Brain(style, seedTwo);
        for (int i = 0; i < seconds * Sim.TicksPerSecond && !d.IsOver; i++)
        {
            var a = one.Think(d, Side.One);
            var b = two.Think(d, Side.Two);
            d.Step(a.Slots, b.Slots, a.Move, b.Move);
        }
        return d.Hits.Select(h => $"{h.Tick} {h.Attacker} {h.Technique.Id} {h.Parry} {h.Clash} {h.Damage}").ToList();
    }

    private static BrainSheet Sheet() => BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv"))).Sheet!;

    [Fact]
    public void Same_seeds_same_fight()
    {
        var monk = Sheet().Find("monk")!;
        Assert.Equal(BrainVsBrain(monk, 1, 2, 30), BrainVsBrain(monk, 1, 2, 30));
    }

    [Theory]
    [InlineData("novice")]
    [InlineData("monk")]
    [InlineData("master")]
    public void Each_sheet_brain_fights_a_copy_of_itself(string id)
    {
        var hits = BrainVsBrain(Sheet().Find(id)!, 1, 2, 60);
        Assert.Contains(hits, h => h.Contains("NotParried") && !h.EndsWith(" 0"));
    }

    [Fact]
    public void The_master_parries_more_than_the_novice()
    {
        int Parries(string id)
        {
            int n = 0;
            for (int seed = 1; seed <= 10; seed++)
                n += BrainVsBrain(Sheet().Find(id)!, seed, seed + 100, 60).Count(h => h.Contains("Parr") && !h.Contains("NotParried"));
            return n;
        }
        Assert.True(Parries("master") > Parries("novice"));
    }

    [Fact]
    public void Dice_rolls_repeat_for_the_same_seed()
    {
        var a = new Dice(7);
        var b = new Dice(7);
        for (int i = 0; i < 100; i++) Assert.Equal(a.Below(1000), b.Below(1000));
        var c = new Dice(0);
        int never = Enumerable.Range(0, 100).Count(_ => c.Chance(0));
        int always = Enumerable.Range(0, 100).Count(_ => c.Chance(100));
        Assert.Equal((0, 100), (never, always));
    }
}
