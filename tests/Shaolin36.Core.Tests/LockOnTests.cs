using System.IO;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Chan: jumping over the opponent with 纵云梯 makes them lose you. They keep going the way they were moving or attacking, and only
/// lock back on after standing still (novice 3 s, monk 1.5 s, master at once), or when they get hit or knocked down. Locking back on
/// they still take a moment to turn round instead of snapping to face you.
/// </summary>
public class LockOnTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);
    private static readonly BrainSheet Brains = BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv"))).Sheet!;

    private static Technique T(string id) => Book.Find(id)!;

    private static Loadout Set()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("huxing_quan_1"));
        l.Assign(Slot.Cross, T("zongyun_ti"));
        l.Assign(Slot.L1, T("timed_parry"));
        return l;
    }

    /// <summary>One jumps over Two from 150 cm; Two can lose its lock and relocks after <paramref name="relock"/> still frames.</summary>
    private static Duel Jumped(int relock, MoveInput twoMove, bool canLose = true)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 150);
        d.Two.CanLoseLock = canLose;
        d.Two.RelockFrames = relock;
        d.Step(SlotInput.Press(Slot.Cross), SlotInput.None);
        while (d.One.IsAirborne) d.Step(SlotInput.None, SlotInput.None, MoveInput.None, twoMove);
        return d;
    }

    /// <summary>How squarely Two faces One: 1 straight at them, -1 straight away.</summary>
    private static double TwoFacesOne(Duel d) =>
        FloorPoint.Dot(d.Two.Facing, d.One.Position - d.Two.Position) / (d.Two.Facing.Length * (d.One.Position - d.Two.Position).Length);

    [Fact]
    public void Landing_behind_a_fighter_that_can_lose_its_lock_leaves_it_facing_the_wrong_way()
    {
        var d = Jumped(90, MoveInput.None);
        Assert.True(d.One.XCm > d.Two.XCm, "landed behind");
        Assert.False(d.Two.IsLockedOn);
        Assert.True(TwoFacesOne(d) < -0.9, "still facing where One took off");
    }

    [Fact]
    public void The_player_never_loses_its_lock_and_turns_at_once_as_before()
    {
        var d = Jumped(90, MoveInput.None, canLose: false);
        d.Step(SlotInput.None, SlotInput.None);
        Assert.True(d.Two.IsLockedOn);
        Assert.True(TwoFacesOne(d) > 0.99);
    }

    [Fact]
    public void Walking_on_keeps_it_lost_and_carries_it_away_the_way_it_was_going()
    {
        var d = Jumped(30, MoveInput.WalkForward);
        int x = d.Two.XCm;
        for (int i = 0; i < 120; i++) d.Step(SlotInput.None, SlotInput.None, MoveInput.None, MoveInput.WalkForward);
        Assert.False(d.Two.IsLockedOn);
        Assert.True(d.Two.XCm < x - 100, $"walked on away from One: {x} -> {d.Two.XCm}");
        Assert.True(TwoFacesOne(d) < -0.9);
    }

    [Fact]
    public void Attacking_in_the_wrong_direction_doesnt_count_as_standing_still()
    {
        var d = Jumped(30, MoveInput.None);
        var strike = T("huxing_quan_1");
        // Its 虎形拳 leaps off the way it faces, away from One, and the hit whiffs.
        var hits = d.Step(SlotInput.None, SlotInput.Press(Slot.Square)).ToList();
        for (int i = 0; i < strike.TotalFrames - 1; i++) hits.AddRange(d.Step(SlotInput.None, SlotInput.None));
        Assert.Empty(hits);
        Assert.False(d.Two.IsLockedOn);
        Assert.True(TwoFacesOne(d) < -0.9);
    }

    [Fact]
    public void Standing_still_its_relock_time_locks_back_on_then_it_turns_round_over_relock_turn_frames()
    {
        const int relock = 30;
        var d = Jumped(relock, MoveInput.None);
        int still = 0;
        while (!d.Two.IsLockedOn) { d.Step(SlotInput.None, SlotInput.None); still++; }
        Assert.Equal(relock + 1, still);
        Assert.True(d.Two.IsTurning);

        int turning = 0;
        while (d.Two.IsTurning) { d.Step(SlotInput.None, SlotInput.None); turning++; Assert.True(turning < 120); }
        Assert.InRange(turning, Rules.RelockTurnFrames - 2, Rules.RelockTurnFrames + 2);
        Assert.True(TwoFacesOne(d) > 0.99);
    }

    [Fact]
    public void Half_way_through_the_turn_it_faces_side_on()
    {
        var d = Jumped(0, MoveInput.None);
        d.Step(SlotInput.None, SlotInput.None);
        Assert.True(d.Two.IsTurning);
        for (int i = 0; i < Rules.RelockTurnFrames / 2; i++) d.Step(SlotInput.None, SlotInput.None);
        Assert.InRange(TwoFacesOne(d), -0.35, 0.35);
    }

    [Fact]
    public void A_relock_of_zero_finds_you_as_soon_as_it_stops()
    {
        var d = Jumped(0, MoveInput.WalkForward);
        Assert.False(d.Two.IsLockedOn);
        d.Step(SlotInput.None, SlotInput.None);
        Assert.True(d.Two.IsLockedOn);
    }

    [Fact]
    public void Getting_hit_locks_it_back_on_at_once()
    {
        var d = Jumped(600, MoveInput.None);
        while (d.One.State != FighterState.Idle) d.Step(SlotInput.None, SlotInput.None);
        Assert.False(d.Two.IsLockedOn);
        var strike = T("huxing_quan_1");
        var hits = d.Step(SlotInput.Press(Slot.Square), SlotInput.None).ToList();
        for (int i = 0; i < strike.StartupFrames + strike.ActiveFrames && !hits.Any(h => h.Landed); i++)
            hits.AddRange(d.Step(SlotInput.None, SlotInput.None));
        Assert.Contains(hits, h => h.Landed && h.Attacker == Side.One);
        Assert.True(d.Two.IsLockedOn);
    }

    [Fact]
    public void The_brain_sheet_gives_novice_3_s_monk_1_5_s_and_master_no_wait()
    {
        Assert.Equal(180, Brains.Find("novice")!.Relock);
        Assert.Equal(90, Brains.Find("monk")!.Relock);
        Assert.Equal(0, Brains.Find("master")!.Relock);
    }

    [Theory]
    [InlineData("novice")]
    [InlineData("monk")]
    [InlineData("master")]
    public void A_brain_that_loses_you_stops_finds_you_by_its_relock_time_and_turns_round(string id)
    {
        var style = Brains.Find(id)!;
        var d = new Duel(Rules, Set(), Set(), 0, 150);
        d.Two.CanLoseLock = true;
        d.Two.RelockFrames = style.Relock;
        var brain = new Brain(style, 7) { Active = true };
        bool lost = false, searched = false;
        int landed = -1, found = -1;
        while (d.Tick < 600 && !d.IsOver)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(d.Tick == 0 ? SlotInput.Press(Slot.Cross) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
            if (!d.Two.IsLockedOn && !lost) { lost = true; landed = d.Tick; }
            if (brain.Intent == BrainIntent.Searching) searched = true;
            if (lost && found < 0 && d.Two.IsLockedOn) found = d.Tick;
        }
        Assert.True(lost, "lost track of the jump");
        Assert.True(found > 0, "found One again");
        // It notices it has lost you within its reaction time, stops, then waits its relock time (plus whatever move it was finishing).
        Assert.InRange(found - landed, style.Relock, style.Relock + style.Reaction + 60);
        if (style.Relock > 0) Assert.True(searched);
    }
}
