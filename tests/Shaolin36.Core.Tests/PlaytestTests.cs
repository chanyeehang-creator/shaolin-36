using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>What the first playable test needs from the rules: walking, the dummy, swappable slots, parry timing info.</summary>
public class PlaytestTests
{
    private static readonly Technique A = Make.Move("a", startup: 10, active: 3, recovery: 16, next: "b", chain: 8, hitstun: 30);
    private static readonly Technique B = Make.Move("b", startup: 9, active: 3, recovery: 16, next: "c", chain: 8, hitstun: 30);
    private static readonly Technique C = Make.Move("c", startup: 16, active: 4, recovery: 28);
    private static readonly Technique Parry = Make.ParryMove();
    private static readonly TechniqueBook Book = new(new[] { A, B, C, Parry });
    private static readonly SlotInput None = SlotInput.None;
    private static readonly MoveInput Forward = MoveInput.WalkForward;
    private static readonly MoveInput Back = MoveInput.WalkBack;
    private static readonly MoveInput Stand = MoveInput.None;

    private static Rules RulesWith(params (string, string)[] extra)
    {
        var all = new List<(string, string)> { ("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"),
            ("walk_speed", "200"), ("min_distance", "80"), ("start_distance", "110"), ("arena_width", "1000"),
            ("dummy_pause", "20"), ("dummy_chain_frame", "0"), ("hp_max", "100000") };
        all.AddRange(extra);
        var r = RulesCsv.Parse(Make.RulesText(all.ToArray()));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique t, Slot slot = Slot.Square)
    {
        var l = new Loadout(Book);
        l.Assign(slot, t);
        return l;
    }

    private static Rules SheetRules() => RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;

    // ---------- Starting a duel ----------

    [Fact]
    public void A_duel_starts_start_distance_apart_with_one_on_the_left()
    {
        var d = Duel.Start(RulesWith(), Holding(A), Holding(A));
        Assert.Equal(110, d.DistanceCm);
        Assert.Equal(-55, d.One.XCm);
        Assert.Equal(55, d.Two.XCm);
    }

    [Fact]
    public void The_real_sheet_starts_out_of_every_attacks_reach()
    {
        // Chan's playtest: a new duel must not open with you getting hit.
        var rules = SheetRules();
        var techniques = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        int longest = techniques.Max(t => t.ReachCm + t.AdvanceCm);
        Assert.True(rules.StartDistance > longest, $"start_distance {rules.StartDistance} should be more than the longest reach {longest}");
        Assert.True(rules.StartDistance <= rules.ArenaWidth);
    }

    // ---------- Walking ----------

    [Fact]
    public void Walking_for_one_second_covers_walk_speed_centimetres()
    {
        var d = Duel.Start(RulesWith(("walk_speed", "175")), Holding(A), Holding(A));
        int start = d.One.XCm;
        for (int i = 0; i < Sim.TicksPerSecond; i++) d.Step(None, None, Back, Stand);
        Assert.Equal(start - 175, d.One.XCm); // 175 cm per second is 175 floor units (1/60 cm) a tick, exactly
        Assert.Equal(55, d.Two.XCm);
    }

    [Fact]
    public void A_fighter_in_a_move_does_not_walk()
    {
        var d = Duel.Start(RulesWith(), Holding(A), Holding(A));
        int start = d.One.XCm;
        d.Step(SlotInput.Press(Slot.Square), None, Back, Stand);
        for (int i = 1; i < A.TotalFrames; i++) d.Step(None, None, Back, Stand);
        Assert.Equal(start, d.One.XCm);
        d.Step(None, None, Back, Stand); // idle again: walks
        Assert.True(d.One.XCm < start);
    }

    [Fact]
    public void Fighters_stop_at_min_distance_and_never_pass_through()
    {
        var d = Duel.Start(RulesWith(), Holding(A), Holding(A));
        for (int i = 0; i < 120; i++) d.Step(None, None, Forward, Stand);
        Assert.Equal(80, d.DistanceCm);
        Assert.Equal(55, d.Two.XCm); // the one who stood still was not pushed
        Assert.True(d.One.XCm < d.Two.XCm);
    }

    [Fact]
    public void Walking_into_each_other_splits_the_overlap()
    {
        var d = Duel.Start(RulesWith(), Holding(A), Holding(A));
        for (int i = 0; i < 120; i++) d.Step(None, None, Forward, Forward);
        Assert.Equal(80, d.DistanceCm);
        Assert.Equal(-40, d.One.XCm);
        Assert.Equal(40, d.Two.XCm);
    }

    [Fact]
    public void Fighters_stop_at_the_arena_edge()
    {
        var d = Duel.Start(RulesWith(("arena_width", "300")), Holding(A), Holding(A));
        for (int i = 0; i < 300; i++) d.Step(None, None, Back, Back);
        Assert.Equal(-150, d.One.XCm);
        Assert.Equal(150, d.Two.XCm);
    }

    [Fact]
    public void Walking_is_deterministic()
    {
        int[] Run()
        {
            var d = Duel.Start(RulesWith(("walk_speed", "137")), Holding(A), Holding(A));
            var seen = new List<int>();
            for (int i = 0; i < 200; i++)
            {
                d.Step(i % 37 == 0 ? SlotInput.Press(Slot.Square) : None, None, new MoveInput(((i / 25) % 3 - 1) * 100, (i / 11) % 3 * 50 - 50), new MoveInput(((i / 17) % 3 - 1) * 100, 0));
                seen.Add(d.One.XCm);
                seen.Add(d.Two.XCm);
            }
            return seen.ToArray();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Walking_out_of_reach_makes_the_hit_whiff()
    {
        var d = Duel.Start(RulesWith(), Holding(A), Holding(A)); // 110 apart, reach 120
        d.Step(None, SlotInput.Press(Slot.Square), Back, Stand);
        for (int i = 1; i < 60; i++) d.Step(None, None, Back, Stand);
        Assert.Empty(d.Hits);
    }

    // ---------- NextState ----------

    [Fact]
    public void NextState_predicts_what_a_tick_without_input_does()
    {
        var f = new Fighter(Holding(A), 100, 0, 0);
        f.Tick(SlotInput.Press(Slot.Square));
        for (int i = 0; i < 80; i++)
        {
            if (i == 50) f.TakeHit(1, 5); // a stun ending, as well as a move ending
            var predicted = f.NextState();
            f.Tick(None);
            Assert.Equal(predicted, f.State);
        }
    }

    // ---------- The training dummy ----------

    /// <summary>Runs the dummy as fighter Two against a fighter One who never presses, far away.</summary>
    private static (Duel, List<(int tick, string id)>) RunDummy(TrainingDummy dummy, int ticks, int distance = 500)
    {
        var d = new Duel(RulesWith(), Holding(Parry), Holding(A), 0, distance);
        var starts = new List<(int, string)>();
        for (int i = 0; i < ticks; i++)
        {
            var before = (d.Two.Technique, d.Two.MoveFrame);
            d.Step(None, dummy.Think(d.Two));
            if (d.Two.MoveFrame == 0 && (before.Technique != d.Two.Technique || before.MoveFrame != 0))
                starts.Add((i, d.Two.Technique!.Id));
        }
        return (d, starts);
    }

    [Fact]
    public void Dummy_throws_the_whole_string_chaining_on_the_first_frame_then_pauses_and_repeats()
    {
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 20, chainFrame: 0);
        var (_, starts) = RunDummy(dummy, 300);

        int aChain = A.StartupFrames + A.ActiveFrames; // first chain frame of a
        int bChain = B.StartupFrames + B.ActiveFrames;
        int first = 20; // it waits the pause before its first string too
        int secondString = first + aChain + bChain + C.TotalFrames + 20;
        Assert.Equal(new[]
        {
            (first, "a"), (first + aChain, "b"), (first + aChain + bChain, "c"),
            (secondString, "a"), (secondString + aChain, "b"), (secondString + aChain + bChain, "c"),
        }, starts.Take(6).ToArray());
    }

    [Fact]
    public void Dummy_chaining_late_starts_each_next_hit_on_that_chain_frame()
    {
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 20, chainFrame: 5);
        var (_, starts) = RunDummy(dummy, 60);
        Assert.Equal((20 + A.StartupFrames + A.ActiveFrames + 5, "b"), starts[1]);
    }

    [Fact]
    public void Dummy_chain_frame_past_the_window_throws_single_hits()
    {
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 10, chainFrame: 8);
        var (_, starts) = RunDummy(dummy, 200);
        Assert.All(starts, s => Assert.Equal("a", s.id));
        Assert.Equal(10, starts[0].tick);
        Assert.Equal(10 + A.TotalFrames + 10, starts[1].tick);
    }

    [Fact]
    public void A_dummy_that_does_not_attack_presses_nothing()
    {
        var dummy = new TrainingDummy(Slot.Square, 0, 0) { Attacks = false };
        var (_, starts) = RunDummy(dummy, 200);
        Assert.Empty(starts);
    }

    [Fact]
    public void Dummy_string_lands_every_hit_when_in_reach()
    {
        var dummy = new TrainingDummy(Slot.Square, pauseFrames: 20, chainFrame: 0);
        var (d, _) = RunDummy(dummy, 80, distance: 100);
        Assert.Equal(new[] { "a", "b", "c" }, d.Hits.Select(h => h.Technique.Id).ToArray());
        Assert.All(d.Hits, h => Assert.Equal(Side.Two, h.Attacker));
    }

    [Fact]
    public void Dummy_from_the_real_sheet_uses_its_numbers()
    {
        var rules = SheetRules();
        var dummy = TrainingDummy.FromRules(rules, Slot.Square);
        Assert.Equal(rules.DummyPause, dummy.PauseFrames);
        Assert.Equal(rules.DummyChainFrame, dummy.ChainFrame);
    }

    // ---------- Swapping slots mid-session ----------

    [Fact]
    public void Swapping_slots_moves_what_a_button_does()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, A);
        l.Assign(Slot.L1, Parry);
        var f = new Fighter(l, 100, 0, 0);

        l.Swap(Slot.L1, Slot.Circle); // parry from L1 to ○
        f.Tick(SlotInput.Press(Slot.L1));
        Assert.Equal(FighterState.Idle, f.State); // L1 is empty now
        f.Tick(SlotInput.Press(Slot.Circle));
        Assert.Same(Parry, f.Technique);
    }

    // ---------- Parry timing info on hits ----------

    [Theory]
    [InlineData(0)]  // parry pressed on the same tick the punch starts
    [InlineData(5)]
    [InlineData(9)]
    public void A_hit_records_what_the_defender_was_doing(int parryDelay)
    {
        var d = new Duel(RulesWith(), Holding(Parry), Holding(A), 0, 100);
        for (int i = 0; i < 30 && d.Hits.Count == 0; i++)
            d.Step(i == parryDelay ? SlotInput.Press(Slot.Square) : None, i == 0 ? SlotInput.Press(Slot.Square) : None);

        var hit = Assert.Single(d.Hits);
        Assert.Same(Parry, hit.DefenderTechnique);
        Assert.Equal(A.StartupFrames - parryDelay, hit.DefenderMoveFrame);
    }

    [Fact]
    public void A_hit_on_an_idle_defender_records_no_move()
    {
        var d = new Duel(RulesWith(), Holding(Parry), Holding(A), 0, 100);
        d.Step(None, SlotInput.Press(Slot.Square));
        for (int i = 1; i < 30; i++) d.Step(None, None);
        var hit = Assert.Single(d.Hits);
        Assert.Null(hit.DefenderTechnique);
        Assert.Equal(-1, hit.DefenderMoveFrame);
    }
}
