using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

public class FighterTests
{
    private static readonly Technique Punch = Make.Move("punch", startup: 10, active: 3, recovery: 12); // 25 frames
    private static readonly Technique Parry = Make.ParryMove();                                     // 3 + 10 + 20

    private static Fighter With(Slot slot, Technique t)
    {
        var f = new Fighter();
        f.Loadout.Assign(slot, t);
        return f;
    }

    /// <summary>Ticks with no input until the fighter is idle. A move started on tick 0 returns its total frames: busy on ticks 0 to total - 1, idle on tick total.</summary>
    private static int TicksUntilIdle(Fighter f)
    {
        int n = 0;
        while (!f.CanAct)
        {
            f.Tick(SlotInput.None);
            n++;
            Assert.True(n < 10_000, "fighter never became idle");
        }
        return n;
    }

    [Fact]
    public void A_new_fighter_is_idle_with_8_empty_slots()
    {
        var f = new Fighter();
        Assert.Equal(FighterState.Idle, f.State);
        Assert.Equal(8, Slots.All.Length);
        Assert.All(Slots.All, s => Assert.True(f.Loadout.IsEmpty(s)));
    }

    [Fact]
    public void Slot_labels_match_the_pad()
    {
        Assert.Equal(new[] { "□", "△", "○", "✕", "L1", "L2", "R1", "R2" }, Slots.All.Select(Slots.Label));
    }

    [Fact]
    public void A_move_runs_exactly_startup_plus_active_plus_recovery_frames()
    {
        var f = With(Slot.Square, Punch);
        var seen = new List<FighterState>();

        f.Tick(SlotInput.Press(Slot.Square));
        while (f.State != FighterState.Idle)
        {
            seen.Add(f.State);
            f.Tick(SlotInput.None);
        }

        Assert.Equal(Punch.TotalFrames, seen.Count);
        Assert.Equal(10, seen.Count(s => s == FighterState.Startup));
        Assert.Equal(3, seen.Count(s => s == FighterState.Active));
        Assert.Equal(12, seen.Count(s => s == FighterState.Recovery));
        // Phases come in order: all startup, then all active, then all recovery.
        Assert.Equal(seen.OrderBy(s => s).ToList(), seen);
    }

    [Fact]
    public void Move_frame_matches_the_technique_timeline()
    {
        var f = With(Slot.Square, Punch);
        f.Tick(SlotInput.Press(Slot.Square));
        for (int frame = 0; frame < Punch.TotalFrames; frame++)
        {
            Assert.Equal(frame, f.MoveFrame);
            Assert.Equal(Fighter.PhaseAt(Punch, frame), f.State);
            Assert.Equal(Punch.IsActive(frame), f.State == FighterState.Active);
            f.Tick(SlotInput.None);
        }
        Assert.Equal(-1, f.MoveFrame);
        Assert.Null(f.Technique);
    }

    [Fact]
    public void Holding_the_button_every_tick_starts_the_next_move_on_the_first_free_tick()
    {
        var f = With(Slot.Square, Punch);
        var starts = new List<int>();
        for (int tick = 0; tick < Punch.TotalFrames * 3; tick++)
        {
            f.Tick(SlotInput.Press(Slot.Square));
            if (f.MoveFrame == 0) starts.Add(tick);
        }
        Assert.Equal(new[] { 0, Punch.TotalFrames, Punch.TotalFrames * 2 }, starts);
    }

    [Fact]
    public void With_no_buffer_presses_while_busy_are_dropped()
    {
        var f = With(Slot.Square, Punch);
        f.Loadout.Assign(Slot.Circle, Parry);
        f.Tick(SlotInput.Press(Slot.Square));
        for (int i = 1; i < Punch.TotalFrames; i++)
        {
            f.Tick(SlotInput.Press(Slot.Circle));
            Assert.Same(Punch, f.Technique);
        }
        f.Tick(SlotInput.Press(Slot.Circle));
        Assert.Same(Parry, f.Technique);
    }

    [Fact]
    public void Pressing_an_empty_slot_does_nothing()
    {
        var f = With(Slot.Square, Punch);
        f.Tick(SlotInput.Press(Slot.R2));
        Assert.Equal(FighterState.Idle, f.State);
    }

    [Theory]
    [InlineData(Slot.Square)]
    [InlineData(Slot.Triangle)]
    [InlineData(Slot.Circle)]
    [InlineData(Slot.Cross)]
    [InlineData(Slot.L1)]
    [InlineData(Slot.L2)]
    [InlineData(Slot.R1)]
    [InlineData(Slot.R2)]
    public void The_parry_works_the_same_on_any_slot(Slot slot)
    {
        var f = With(slot, Parry);
        f.Tick(SlotInput.Press(slot));
        Assert.Same(Parry, f.Technique);
        Assert.Equal(slot, f.Slot);
        Assert.Equal(Parry.TotalFrames, TicksUntilIdle(f));
    }

    [Theory]
    [InlineData(Slot.Square)]
    [InlineData(Slot.Cross)]
    [InlineData(Slot.L2)]
    [InlineData(Slot.R1)]
    public void An_attack_works_the_same_on_any_slot(Slot slot)
    {
        var f = With(slot, Punch);
        f.Tick(SlotInput.Press(slot));
        Assert.Same(Punch, f.Technique);
        Assert.Equal(Punch.TotalFrames, TicksUntilIdle(f));
    }

    [Fact]
    public void Swapping_slots_moves_the_technique_to_the_other_button()
    {
        var f = With(Slot.L1, Parry);
        f.Loadout.Assign(Slot.Circle, Punch);
        f.Loadout.Swap(Slot.L1, Slot.Circle);

        f.Tick(SlotInput.Press(Slot.Circle));
        Assert.Same(Parry, f.Technique);
        TicksUntilIdle(f);
        f.Tick(SlotInput.Press(Slot.L1));
        Assert.Same(Punch, f.Technique);
    }

    [Fact]
    public void Cleared_slot_stops_working()
    {
        var f = With(Slot.L1, Parry);
        f.Loadout.Clear(Slot.L1);
        f.Tick(SlotInput.Press(Slot.L1));
        Assert.Equal(FighterState.Idle, f.State);
    }

    [Fact]
    public void Two_slots_on_one_tick_pick_the_first_in_pad_order()
    {
        var f = With(Slot.R2, Punch);
        f.Loadout.Assign(Slot.Triangle, Parry);
        f.Tick(SlotInput.Press(Slot.R2).With(Slot.Triangle));
        Assert.Equal(Slot.Triangle, f.Slot);
    }

    [Theory]
    [InlineData(FighterState.Hitstun, 18)]
    [InlineData(FighterState.Stagger, 30)]
    [InlineData(FighterState.Recoil, 14)]
    [InlineData(FighterState.Recoil, 1)]
    public void A_stun_of_N_frames_frees_the_fighter_N_ticks_later(FighterState stun, int frames)
    {
        var f = With(Slot.Square, Punch);
        f.Tick(SlotInput.Press(Slot.Square));
        f.Stun(stun, frames); // lands on this tick, cancelling the punch

        Assert.Equal(stun, f.State);
        Assert.Null(f.Technique);
        for (int i = 1; i < frames; i++)
        {
            f.Tick(SlotInput.Press(Slot.Square)); // mashing does nothing while stunned
            Assert.Equal(stun, f.State);
        }
        f.Tick(SlotInput.Press(Slot.Square)); // tick t + N: free, and the press starts a move
        Assert.Equal(FighterState.Startup, f.State);
        Assert.Equal(0, f.MoveFrame);
    }

    [Fact]
    public void Only_hitstun_stagger_and_recoil_are_stuns()
    {
        var f = new Fighter();
        Assert.Throws<ArgumentException>(() => f.Stun(FighterState.Active, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Stun(FighterState.Hitstun, 0));
    }

    [Fact]
    public void The_same_inputs_give_the_same_fight_every_time()
    {
        // A fixed, seeded script of presses and stuns.
        var rng = new Random(36);
        var script = new List<(SlotInput input, int stun)>();
        for (int i = 0; i < 2_000; i++)
        {
            var input = rng.Next(4) == 0 ? SlotInput.Press(Slots.All[rng.Next(8)]) : SlotInput.None;
            int stun = rng.Next(40) == 0 ? 1 + rng.Next(40) : 0;
            script.Add((input, stun));
        }

        List<string> Run()
        {
            var f = new Fighter();
            f.Loadout.Assign(Slot.Square, Punch);
            f.Loadout.Assign(Slot.L1, Parry);
            f.Loadout.Assign(Slot.R2, Make.Move("heavy", Weight.Heavy, startup: 16, active: 4, recovery: 28));
            var log = new List<string>();
            foreach (var (input, stun) in script)
            {
                f.Tick(input);
                if (stun > 0) f.Stun(FighterState.Hitstun, stun);
                log.Add(f.ToString());
            }
            return log;
        }

        var first = Run();
        Assert.Equal(first, Run());
        Assert.Contains(first, s => s.StartsWith("Active"));  // the script actually exercised moves
        Assert.Contains(first, s => s.StartsWith("Hitstun")); // and stuns
    }

    [Fact]
    public void Real_sheet_techniques_run_their_sheet_length_on_any_slot()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        foreach (var t in sheet)
        {
            foreach (var slot in Slots.All)
            {
                var f = With(slot, t);
                f.Tick(SlotInput.Press(slot));
                Assert.Equal(t.TotalFrames, TicksUntilIdle(f));
            }
        }
    }
}
