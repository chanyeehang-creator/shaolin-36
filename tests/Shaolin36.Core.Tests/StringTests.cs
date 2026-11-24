using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>Chaining a string by pressing the same slot again, and the input buffer.</summary>
public class StringTests
{
    // A 3-hit test string: a -> b -> c. Recovery 16 with an 8-frame chain window on a and b.
    private static readonly Technique A = Make.Move("a", startup: 10, active: 3, recovery: 16, next: "b", chain: 8);
    private static readonly Technique B = Make.Move("b", startup: 9, active: 3, recovery: 16, next: "c", chain: 8);
    private static readonly Technique C = Make.Move("c", startup: 16, active: 4, recovery: 28);
    private static readonly Technique Parry = Make.ParryMove();
    private static readonly TechniqueBook Book = new(new[] { A, B, C, Parry });

    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);
    private static readonly SlotInput None = SlotInput.None;

    private static Fighter Fighter(int buffer = 0)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, A);
        l.Assign(Slot.L1, Parry);
        return new Fighter(l, inputBufferFrames: buffer);
    }

    /// <summary>Tick with no input until the fighter is on recovery frame <paramref name="c"/> of its current move, not ticking that frame.</summary>
    private static void RunToJustBeforeRecoveryFrame(Fighter f, int c)
    {
        var t = f.Technique!;
        int target = t.StartupFrames + t.ActiveFrames + c;
        while (f.MoveFrame < target - 1) f.Tick(None);
    }

    private static Dictionary<string, Technique> Sheet() =>
        TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques.ToDictionary(t => t.Id);

    private static Rules SheetRules() => RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;

    // ---------- Fighter: chain windows ----------

    [Fact]
    public void Pressing_the_same_slot_on_the_first_chain_frame_starts_the_next_hit_on_that_tick()
    {
        var f = Fighter();
        f.Tick(Square);
        Assert.Equal(1, f.StringHit);
        RunToJustBeforeRecoveryFrame(f, 0);
        Assert.False(f.InChainWindow);

        f.Tick(Square); // recovery frame 0
        Assert.Same(B, f.Technique);
        Assert.Equal(0, f.MoveFrame);
        Assert.Equal(Slot.Square, f.Slot);
        Assert.Equal(2, f.StringHit);
    }

    [Fact]
    public void Chaining_through_the_whole_string_plays_every_hit_then_stops()
    {
        var f = Fighter();
        var started = new List<string>();
        f.Tick(Square);
        started.Add(f.Technique!.Id);
        for (int i = 0; i < 300; i++)
        {
            var before = f.Technique;
            f.Tick(f.InChainWindow ? Square : None);
            if (f.Technique != null && f.Technique != before && f.MoveFrame == 0) started.Add(f.Technique.Id);
        }
        Assert.Equal(new[] { "a", "b", "c" }, started);
        Assert.Equal(FighterState.Idle, f.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(7)] // the last chain frame still chains
    public void Any_frame_of_the_chain_window_chains(int c)
    {
        var f = Fighter();
        f.Tick(Square);
        RunToJustBeforeRecoveryFrame(f, c);
        f.Tick(Square);
        Assert.Same(B, f.Technique);
    }

    [Fact]
    public void One_frame_after_the_window_the_string_is_over()
    {
        var f = Fighter(); // no buffer
        f.Tick(Square);
        RunToJustBeforeRecoveryFrame(f, 8);
        f.Tick(Square); // recovery frame 8: window closed, press dropped
        Assert.Same(A, f.Technique);
        Assert.False(f.InChainWindow);

        while (!f.CanAct) f.Tick(None);
        f.Tick(Square);
        Assert.Same(A, f.Technique); // a fresh press starts the string again from hit 1
        Assert.Equal(1, f.StringHit);
    }

    [Fact]
    public void Another_slot_in_the_chain_window_does_not_chain_or_cancel()
    {
        var f = Fighter();
        f.Tick(Square);
        RunToJustBeforeRecoveryFrame(f, 0);
        f.Tick(SlotInput.Press(Slot.L1));
        Assert.Same(A, f.Technique);
        Assert.Equal(FighterState.Recovery, f.State);
    }

    [Fact]
    public void The_last_hit_has_no_chain_window()
    {
        var f = Fighter();
        f.Tick(Square);
        for (int hit = 0; hit < 2; hit++)
        {
            RunToJustBeforeRecoveryFrame(f, 0);
            f.Tick(Square);
        }
        Assert.Same(C, f.Technique);
        RunToJustBeforeRecoveryFrame(f, 0);
        for (int i = 0; i < C.RecoveryFrames; i++)
        {
            Assert.False(f.InChainWindow);
            f.Tick(Square);
            if (f.CanAct) break;
        }
        Assert.Same(C, f.Technique);
    }

    [Fact]
    public void A_whiffed_hit_still_chains()
    {
        // No opponent at all: nothing lands, and the string still plays out.
        var f = Fighter();
        f.Tick(Square);
        RunToJustBeforeRecoveryFrame(f, 0);
        Assert.False(f.HitLanded);
        f.Tick(Square);
        Assert.Same(B, f.Technique);
    }

    [Fact]
    public void Chaining_without_a_technique_book_is_an_error()
    {
        var l = new Loadout();
        l.Assign(Slot.Square, A);
        var f = new Fighter(l);
        f.Tick(Square);
        RunToJustBeforeRecoveryFrame(f, 0);
        var e = Assert.Throws<InvalidOperationException>(() => f.Tick(Square));
        Assert.Contains("no technique book", e.Message);
    }

    // ---------- Fighter: input buffer ----------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)] // pressed 6 ticks early with a 6-frame buffer: still used on recovery frame 0
    public void An_early_press_within_the_buffer_chains_on_the_first_chain_frame(int early)
    {
        var f = Fighter(buffer: 6);
        f.Tick(Square);
        int firstChainTick = A.StartupFrames + A.ActiveFrames; // tick of recovery frame 0
        for (int tick = 1; tick < firstChainTick; tick++) f.Tick(tick == firstChainTick - early ? Square : None);
        Assert.Same(A, f.Technique);
        Assert.Equal(Square, f.Buffered);

        f.Tick(None);
        Assert.Same(B, f.Technique);
        Assert.Equal(0, f.MoveFrame);
        Assert.Equal(None, f.Buffered);
    }

    [Fact]
    public void A_press_older_than_the_buffer_is_dropped()
    {
        var f = Fighter(buffer: 6);
        f.Tick(Square);
        int firstChainTick = A.StartupFrames + A.ActiveFrames;
        for (int tick = 1; tick < firstChainTick; tick++) f.Tick(tick == firstChainTick - 7 ? Square : None);
        Assert.Equal(None, f.Buffered);
        f.Tick(None);
        Assert.Same(A, f.Technique);
        Assert.Equal(FighterState.Recovery, f.State);
    }

    [Fact]
    public void A_press_during_recovery_after_the_window_starts_the_next_move_on_the_first_free_tick()
    {
        var f = Fighter(buffer: 6);
        f.Loadout.Clear(Slot.Square);
        f.Loadout.Assign(Slot.Square, C); // no string, recovery 28
        f.Tick(Square);
        while (f.MoveFrame < C.TotalFrames - 3) f.Tick(None);
        f.Tick(SlotInput.Press(Slot.L1)); // 2 frames of recovery left after this one
        Assert.Same(C, f.Technique);
        f.Tick(None);
        f.Tick(None); // free on this tick: the buffered parry starts
        Assert.Same(Parry, f.Technique);
        Assert.Equal(0, f.MoveFrame);
    }

    [Fact]
    public void A_press_during_hitstun_comes_out_when_the_stun_ends()
    {
        var f = Fighter(buffer: 6);
        f.TakeHit(1, 18);
        for (int i = 1; i < 18; i++) f.Tick(i == 15 ? SlotInput.Press(Slot.L1) : None);
        Assert.Equal(FighterState.Hitstun, f.State);
        f.Tick(None); // tick 18: free, and the buffered parry starts
        Assert.Same(Parry, f.Technique);
    }

    [Fact]
    public void Only_the_newest_unused_press_is_kept()
    {
        var f = Fighter(buffer: 6);
        f.Tick(Square);
        int firstChainTick = A.StartupFrames + A.ActiveFrames;
        for (int tick = 1; tick < firstChainTick; tick++)
        {
            var input = tick == firstChainTick - 4 ? Square : tick == firstChainTick - 2 ? SlotInput.Press(Slot.L1) : None;
            f.Tick(input);
        }
        Assert.Equal(SlotInput.Press(Slot.L1), f.Buffered);
        f.Tick(None); // the Square press was replaced, so no chain
        Assert.Same(A, f.Technique);
    }

    [Fact]
    public void With_no_buffer_an_early_press_does_not_chain()
    {
        // Press on the last active frame, one tick before the window opens.
        var g = Fighter(buffer: 0);
        g.Tick(Square);
        RunToJustBeforeRecoveryFrame(g, -1);
        g.Tick(Square);
        g.Tick(None);
        Assert.Same(A, g.Technique);
        Assert.Equal(None, g.Buffered);
    }

    [Fact]
    public void Negative_buffer_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(inputBufferFrames: -1));
    }

    // ---------- TechniqueBook ----------

    [Fact]
    public void The_book_finds_the_next_hit_and_reports_a_missing_one()
    {
        Assert.Same(B, Book.NextOf(A));
        Assert.Null(Book.NextOf(C));
        Assert.Null(Book.Find("nope"));
        var lonely = new TechniqueBook(new[] { A });
        Assert.Throws<InvalidOperationException>(() => lonely.NextOf(A));
        Assert.Throws<ArgumentException>(() => new TechniqueBook(new[] { A, A }));
    }

    // ---------- Duel with the real sheets: the 罗汉拳 string ----------

    /// <summary>
    /// One (罗汉拳 on □) attacks Two (who does nothing) from 100 cm. One presses on tick 0,
    /// then presses again on recovery frame <paramref name="chainFrame"/> of each hit.
    /// Returns the duel and, for each hit after the first, whether Two had a free tick before it landed.
    /// </summary>
    private static (Duel duel, List<bool> gaps) RunLuohanQuan(int chainFrame)
    {
        var sheet = Sheet();
        var book = new TechniqueBook(sheet.Values);
        var one = new Loadout(book);
        one.Assign(Slot.Square, sheet["luohan_quan_1"]);
        var d = new Duel(SheetRules(), one, new Loadout(book), 0, 100);

        var gaps = new List<bool>();
        bool twoWasFree = false;
        d.Step(Square, None);
        for (int tick = 1; tick < 400 && !d.IsOver; tick++)
        {
            var f = d.One;
            // Press on the tick the fighter reaches recovery frame chainFrame: it advances first, then acts.
            bool press = f.State.IsInMove()
                && f.MoveFrame == f.Technique!.StartupFrames + f.Technique.ActiveFrames + chainFrame - 1;
            // Two acts before hits land within a tick, so Two is free on this tick if idle or on the last stun frame.
            if (d.Hits.Count > 0 && FreeThisTick(d.Two)) twoWasFree = true;
            var landed = d.Step(press ? Square : None, None);
            if (landed.Count > 0 && d.Hits.Count > 1) gaps.Add(twoWasFree);
            if (landed.Count > 0) twoWasFree = false;
        }
        return (d, gaps);
    }

    /// <summary>True if the fighter can act on the next tick, before any hit on that tick lands.</summary>
    private static bool FreeThisTick(Fighter f) => f.CanAct || (f.State.IsStunned() && f.StunFramesLeft == 1);

    [Fact]
    public void Chaining_on_the_first_frame_links_all_4_hits()
    {
        var (d, gaps) = RunLuohanQuan(chainFrame: 0);

        Assert.Equal(new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" },
            d.Hits.Select(h => h.Technique.Id));
        Assert.Equal(new[] { false, false, false }, gaps); // Two never got a free tick between hits
        Assert.Equal(31, d.Hits.Sum(h => h.Damage));
        Assert.Equal(120 - 31, d.Two.Hp);
    }

    [Fact]
    public void The_string_has_the_playtested_rhythm()
    {
        // From Chan's second playtest: a 0.32 s wind-up, then 0.5 s, 0.2 s and 0.5 s between hits.
        var (d, gaps) = RunLuohanQuan(chainFrame: 0);
        var ticks = d.Hits.Select(h => h.Tick).ToArray();
        Assert.Equal(new[] { 19, 49, 61, 91 }, ticks);
        Assert.Equal(0.32, Sim.FramesToSeconds(ticks[0]), 2);
        Assert.Equal(0.5, Sim.FramesToSeconds(ticks[1] - ticks[0]), 2);
        Assert.Equal(0.2, Sim.FramesToSeconds(ticks[2] - ticks[1]), 2);
        Assert.Equal(0.5, Sim.FramesToSeconds(ticks[3] - ticks[2]), 2);
        Assert.Equal(new[] { false, false, false }, gaps); // still a true combo
    }

    [Fact]
    public void Chaining_late_leaves_a_gap()
    {
        var (d, gaps) = RunLuohanQuan(chainFrame: 7); // last frame of each 8-frame window
        Assert.Equal(4, d.Hits.Count);
        Assert.All(gaps, g => Assert.True(g)); // Two is free before every follow-up: room to parry or clash
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Each_chain_frame_links_exactly_when_the_link_numbers_say_so(int chainFrame)
    {
        var sheet = Sheet();
        var (d, gaps) = RunLuohanQuan(chainFrame);
        Assert.Equal(4, d.Hits.Count);
        var expected = new List<bool>();
        for (var t = sheet["luohan_quan_1"]; t.HasNext; t = sheet[t.Next])
            expected.Add(chainFrame >= t.LinkFramesInto(sheet[t.Next])); // a gap once past the link frames
        Assert.Equal(expected, gaps);
    }

    [Fact]
    public void Mashing_the_button_with_the_sheet_buffer_links_the_whole_string()
    {
        var sheet = Sheet();
        var book = new TechniqueBook(sheet.Values);
        var one = new Loadout(book);
        one.Assign(Slot.Square, sheet["luohan_quan_1"]);
        var d = new Duel(SheetRules(), one, new Loadout(book), 0, 100);

        // Tap every 4th tick, like a player mashing about 15 times a second.
        bool twoFreeBetweenHits = false;
        for (int tick = 0; d.Hits.Count < 4 && tick < 300; tick++)
        {
            if (d.Hits.Count is > 0 and < 4 && FreeThisTick(d.Two)) twoFreeBetweenHits = true;
            d.Step(tick % 4 == 0 ? Square : None, None);
        }
        Assert.Equal(4, d.Hits.Count);
        Assert.False(twoFreeBetweenHits);
    }

    [Fact]
    public void The_sheet_has_a_short_input_buffer()
    {
        var rules = SheetRules();
        Assert.InRange(rules.InputBuffer, 1, 10);
    }
}
