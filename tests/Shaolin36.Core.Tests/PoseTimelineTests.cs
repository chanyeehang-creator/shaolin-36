using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Poses;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Moves with more than one key pose (".windup@30", ".strike@100", ".recovery@50"), wind-ups that flow instead of
/// holding, and the whole-body turn that makes rolls and flips (the <see cref="Joint.Body"/> joint).
/// </summary>
public class PoseTimelineTests
{
    private const string Header = "pose,joint,pitch,yaw,roll\n";

    /// <summary>Every base pose (the stance with arm_r at 0), plus <paramref name="keys"/>.</summary>
    private static PoseSheet Sheet(string keys)
    {
        var text = Header;
        int n = 1;
        foreach (var name in PoseSheet.BasePoses) text += $"{name},chest,{n++},0,0\n";
        var r = PoseCsv.Parse(text + keys);
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!;
    }

    private static int ArmAt(PoseSheet sheet, Technique t, int frame) => sheet.MovePose(t, frame).Pitch(Joint.ArmR);

    // ---------- Timing the keys ----------

    [Fact]
    public void A_wind_up_at_100_never_holds_and_reaches_its_pose_on_the_last_startup_frame()
    {
        var sheet = Sheet("t.windup@100,arm_r,-60,0,0\nt.strike,arm_r,90,0,0\n");
        var t = Make.Move("t", startup: 14, active: 3, recovery: 12);
        Assert.True(sheet.TimelineOf(t).Flows);
        int last = 0;
        for (int frame = 0; frame < t.StartupFrames; frame++)
        {
            int arm = ArmAt(sheet, t, frame);
            Assert.True(arm < last, $"frame {frame}: arm at {arm}, was {last} (stopped)");
            last = arm;
        }
        Assert.Equal(-60, last);                         // peak on the last startup frame...
        Assert.Equal(90, ArmAt(sheet, t, t.StartupFrames)); // ...and the strike the very next
    }

    [Fact]
    public void A_plain_wind_up_still_moves_in_and_holds_cocked()
    {
        var sheet = Sheet("t.windup,arm_r,-60,0,0\nt.strike,arm_r,90,0,0\n");
        var t = Make.Move("t", startup: 14, active: 3, recovery: 12);
        Assert.False(sheet.TimelineOf(t).Flows);
        int reached = MoveTimeline.WindUpReachedAfter(t);
        for (int frame = reached - 1; frame < t.StartupFrames; frame++) Assert.Equal(-60, ArmAt(sheet, t, frame));
        Assert.True(ArmAt(sheet, t, 0) > -10); // eases off the stance
    }

    [Fact]
    public void Wind_up_keys_are_reached_at_their_share_of_the_startup()
    {
        var sheet = Sheet("t.windup@30,arm_r,-30,0,0\nt.windup@100,arm_r,-90,0,0\nt.strike,arm_r,90,0,0\n");
        var t = Make.Move("t", startup: 10, active: 3, recovery: 12);
        var timeline = sheet.TimelineOf(t);
        Assert.Equal(-1, timeline.FrameOf(0)); // the entry
        Assert.Equal(2, timeline.FrameOf(1));  // 30% of 10 frames: the 3rd
        Assert.Equal(9, timeline.FrameOf(2));  // 100%: the last
        Assert.Equal(-30, ArmAt(sheet, t, 2));
        Assert.Equal(-90, ArmAt(sheet, t, 9));
        // Through the middle key it keeps its speed: no slowing to a stop at -30.
        Assert.True(ArmAt(sheet, t, 3) - ArmAt(sheet, t, 2) <= -8);
    }

    [Fact]
    public void Strike_keys_carry_the_hit_on_through_the_active_frames()
    {
        var sheet = Sheet("t.windup,arm_r,-60,0,0\nt.strike,arm_r,90,0,0\nt.strike@100,arm_r,150,0,0\n");
        var t = Make.Move("t", startup: 10, active: 6, recovery: 12);
        Assert.Equal(90, ArmAt(sheet, t, 10));  // snaps in on the first active frame
        Assert.Equal(150, ArmAt(sheet, t, 15)); // the last
        for (int frame = 11; frame <= 15; frame++)
            Assert.True(ArmAt(sheet, t, frame) > ArmAt(sheet, t, frame - 1), $"active frame {frame}: stopped");
    }

    [Fact]
    public void Recovery_keys_are_passed_on_the_way_back_to_the_stance()
    {
        var sheet = Sheet("t.windup,arm_r,-60,0,0\nt.strike,arm_r,90,0,0\nt.recovery@50,arm_r,40,0,0\n");
        var t = Make.Move("t", startup: 10, active: 3, recovery: 12);
        Assert.Equal(40, ArmAt(sheet, t, 13 + 5)); // 50% of 12 recovery frames: the 6th
        Assert.True(sheet.MovePose(t, t.TotalFrames - 1).SameAngles(sheet.Stance));
    }

    [Fact]
    public void A_key_that_repeats_the_one_before_holds_still_then_eases_out_of_it()
    {
        // The strike pose repeated at 50% of the recovery: the finish is held, then the body eases back to the stance.
        var sheet = Sheet("t.windup@100,arm_r,-60,0,0\nt.strike,arm_r,90,0,0\nt.recovery@50,arm_r,90,0,0\n");
        var t = Make.Move("t", startup: 10, active: 2, recovery: 20);
        int held = 12 + 9; // the 10th of 20 recovery frames
        for (int frame = 10; frame <= held; frame++) Assert.Equal(90, ArmAt(sheet, t, frame));
        int Step(int frame) => ArmAt(sheet, t, frame - 1) - ArmAt(sheet, t, frame);
        for (int frame = held + 1; frame < t.TotalFrames; frame++) Assert.True(Step(frame) > 0, $"frame {frame}: stopped");
        Assert.True(Step(held + 1) < Step(held + 5), "should ease out of the hold, not jerk out of it");
        Assert.True(sheet.MovePose(t, t.TotalFrames - 1).SameAngles(sheet.Stance));
    }

    [Fact]
    public void A_move_uses_its_own_keys_for_a_part_and_the_category_for_the_rest()
    {
        var sheet = Sheet("waigong.windup,arm_r,-40,0,0\nwaigong.strike,arm_r,90,0,0\nt.strike,arm_r,120,0,0\n");
        var t = Make.Move("t");
        Assert.Equal(-40, sheet.WindUpOf(t).Pitch(Joint.ArmR));
        Assert.Equal(120, sheet.StrikeOf(t).Pitch(Joint.ArmR));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void The_body_slows_only_where_it_stops(bool fromRest, bool toRest)
    {
        Assert.Equal(0, MoveTimeline.Shape(0, fromRest, toRest));
        Assert.Equal(1000, MoveTimeline.Shape(1000, fromRest, toRest));
        for (int x = 1; x <= 1000; x++) Assert.True(MoveTimeline.Shape(x, fromRest, toRest) >= MoveTimeline.Shape(x - 1, fromRest, toRest));
        int start = MoveTimeline.Shape(100, fromRest, toRest), end = MoveTimeline.Shape(900, fromRest, toRest);
        // Slow at an end that stops; at an end that doesn't, at least the steady speed (no slowing down there).
        if (fromRest) Assert.True(start < 100, "should ease off a stop"); else Assert.True(start >= 100, "slowed at a moving start");
        if (toRest) Assert.True(end > 900, "should ease into a stop"); else Assert.True(end <= 900, "slowed at a moving end");
        if (!fromRest && !toRest) Assert.Equal(500, MoveTimeline.Shape(500, false, false)); // steady between moving keys
    }

    // ---------- The whole-body turn ----------

    [Fact]
    public void The_body_turns_up_to_a_full_turn_either_way()
    {
        var r = PoseCsv.Parse(Header + "stance,body,360,-360,90\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(360, r.Sheet!.Stance.Pitch(Joint.Body));

        r = PoseCsv.Parse(Header + "stance,body,361,0,0\n");
        Assert.Contains(r.Errors, e => e.Contains("from -360 to 360"));
        r = PoseCsv.Parse(Header + "stance,arm_r,200,0,0\n");
        Assert.Contains(r.Errors, e => e.Contains("from -180 to 180")); // the limbs still stop at half a turn
    }

    private static Pose BodyAt(int pitch) =>
        PoseCsv.Parse(Header + $"stance,body,{pitch},0,0\n").Sheet!.Stance;

    [Fact]
    public void The_body_turns_the_short_way_round()
    {
        // From 360 (a roll just done) to 0 is no turn at all: it doesn't spin back.
        for (int p = 0; p <= 1000; p += 100) Assert.Equal(0, Pose.Blend(BodyAt(360), BodyAt(0), p).Pitch(Joint.Body) % 360);
        // From 330 to 0 goes on forward the last 30 degrees.
        Assert.Equal(345, (Pose.Blend(BodyAt(330), BodyAt(0), 500).Pitch(Joint.Body) + 360) % 360);
        // Up to half a turn between keys, it goes the way the keys say.
        Assert.Equal(260, Pose.Blend(BodyAt(200), BodyAt(320), 500).Pitch(Joint.Body));
    }

    [Fact]
    public void A_pose_is_turned_over_unless_the_body_is_at_whole_turns()
    {
        Assert.False(BodyAt(0).IsTurnedOver);
        Assert.False(BodyAt(360).IsTurnedOver);
        Assert.True(BodyAt(70).IsTurnedOver);
        Assert.True(PoseCsv.Parse(Header + "stance,body,0,0,90\n").Sheet!.Stance.IsTurnedOver); // a cartwheel
        Assert.False(PoseCsv.Parse(Header + "stance,hips,0,90,0\n").Sheet!.Stance.IsTurnedOver); // the hips alone don't count
    }

    // ---------- The check ----------

    [Theory]
    [InlineData("t.wiggle,arm_r,1,0,0", "must end in .windup, .strike or .recovery@<percent>")]
    [InlineData("t.windup@101,arm_r,1,0,0", "a wind-up key is @0 to @100")]
    [InlineData("t.windup@half,arm_r,1,0,0", "must be a whole percent")]
    [InlineData("t.strike@0,arm_r,1,0,0", "a strike key is @1 to @100")]
    [InlineData("t.recovery,arm_r,1,0,0", "a recovery key needs its percent")]
    [InlineData("t.recovery@100,arm_r,1,0,0", "a recovery key is @1 to @99")]
    [InlineData("t.windup@50,arm_r,1,0,0\nt.windup,arm_r,2,0,0", "are both 50% through")]
    [InlineData("u.strike@100,arm_r,1,0,0", "has strike keys but no 'u.strike'")]
    [InlineData("nobody.windup@30,arm_r,1,0,0", "no technique or category called 'nobody'")]
    public void The_check_finds_bad_key_names(string rows, string expected)
    {
        var sheet = Sheet("waigong.windup,arm_r,-40,0,0\nwaigong.strike,arm_r,90,0,0\n" + rows + "\n");
        var errors = sheet.Check(new[] { Make.Move("t"), Make.Move("u") });
        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void Good_key_names_pass_the_check()
    {
        var sheet = Sheet("waigong.windup,arm_r,-40,0,0\nwaigong.strike,arm_r,90,0,0\n" +
                          "t.windup@0,arm_r,1,0,0\nt.windup@100,arm_r,2,0,0\nt.strike,arm_r,3,0,0\nt.strike@100,arm_r,4,0,0\n" +
                          "t.recovery@1,arm_r,5,0,0\nt.recovery@99,arm_r,6,0,0\n");
        var errors = sheet.Check(new[] { Make.Move("t") });
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    // ---------- Moves on the game's sheets ----------

    /// <summary>
    /// The string <paramref name="first"/> starts as a fight plays it (each next hit chained on its first chain frame), one
    /// pose per tick, each move starting from the pose the last one left off in, as the mannequin does.
    /// </summary>
    private static List<(int hit, FighterState state, int frame, Pose pose)> PlayString(string first, int hits)
    {
        var techniques = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var poses = PoseCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("poses.csv")));
        Assert.True(poses.Ok, string.Join("\n", poses.Errors));
        var sheet = poses.Sheet!;
        var book = new TechniqueBook(techniques);
        var mine = new Loadout(book);
        var move = book.Find(first)!;
        mine.Assign(Slot.Square, move);
        var d = new Duel(rules, mine, new Loadout(book), 0, Math.Max(rules.MinDistance + 10, move.FullReachCm - 20));
        var me = d.One;

        var shown = new List<(int, FighterState, int, Pose)>();
        Pose? last = null, entry = null;
        Technique? lastMove = null;
        for (int tick = 0; tick < 400; tick++)
        {
            d.Step(tick == 0 || me.InChainWindow ? SlotInput.Press(Slot.Square) : SlotInput.None, SlotInput.None);
            if (tick > 0 && !me.State.IsInMove()) break;
            if (!ReferenceEquals(me.Technique, lastMove)) entry = last ?? sheet.Stance;
            lastMove = me.Technique;
            last = sheet.PoseAt(me, entry);
            shown.Add((me.StringHit, me.State, me.MoveFrame, last));
        }
        Assert.Equal(hits, shown[shown.Count - 1].Item1); // every hit played
        return shown;
    }

    private static List<(int hit, FighterState state, int frame, Pose pose)> TigerString() => PlayString("huxing_quan_1", 3);

    private static int FirstIndex(List<(int hit, FighterState state, int frame, Pose pose)> s, int hit, FighterState state) =>
        s.FindIndex(x => x.hit == hit && x.state == state);

    [Fact]
    public void Huxing_quan_holds_the_tiger_pose_then_never_stops_until_hit_2_lands()
    {
        var s = TigerString();
        // Hit 1: the tiger pose is reached and held, still, until the pounce.
        int pounce = FirstIndex(s, 1, FighterState.Active);
        Assert.True(s[pounce - 1].pose.SameAngles(s[pounce - 4].pose), "the tiger pose should hold before the pounce");
        // From the pounce, through the roll, to hit 2's strike, the body moves on every frame and keeps rolling forward
        // (the whole body turns one way only, a full turn by the time it's up).
        int clawLands = FirstIndex(s, 2, FighterState.Active);
        int turned = 0;
        for (int i = pounce + 1; i <= clawLands; i++)
        {
            Assert.False(s[i].pose.SameAngles(s[i - 1].pose), $"tick {i} (hit {s[i].hit} {s[i].state} {s[i].frame}): stopped");
            int step = ((s[i].pose.Pitch(Joint.Body) - s[i - 1].pose.Pitch(Joint.Body)) % 360 + 540) % 360 - 180;
            Assert.True(step >= 0, $"tick {i}: the roll turned back {step}");
            turned += step;
        }
        Assert.Equal(360, turned);
        Assert.False(s[clawLands].pose.IsTurnedOver);
    }

    [Fact]
    public void Huxing_quan_3_winds_up_without_a_stop_and_strikes_at_the_peak()
    {
        var s = TigerString();
        int windUp = FirstIndex(s, 3, FighterState.Startup), strike = FirstIndex(s, 3, FighterState.Active);
        for (int i = windUp; i <= strike; i++)
            Assert.False(s[i].pose.SameAngles(s[i - 1].pose), $"hit 3 tick {i - windUp}: stopped");
        // The leg rises all the way to its peak on the last wind-up frame, then the stamp snaps out.
        for (int i = windUp + 1; i < strike; i++)
            Assert.True(s[i].pose.Pitch(Joint.LegR) >= s[i - 1].pose.Pitch(Joint.LegR), $"hit 3 tick {i - windUp}: the leg dropped");
        Assert.Equal(95, s[strike - 1].pose.Pitch(Joint.LegR));
    }

    [Fact]
    public void Tiesha_zhang_winds_up_without_a_stop_pauses_briefly_at_the_top_strikes_then_holds_it_before_easing_back()
    {
        var s = PlayString("tiesha_zhang", 1);
        int strike = FirstIndex(s, 1, FighterState.Active), recovery = FirstIndex(s, 1, FighterState.Recovery);
        // The wind-up moves on every frame until it reaches the raised palm, then pauses there briefly before the strike.
        var top = s[strike - 1].pose;
        int reached = strike - 1;
        while (reached > 0 && s[reached - 1].pose.SameAngles(top)) reached--;
        Assert.InRange(strike - reached, 5, 10);
        for (int i = 1; i <= reached; i++)
            Assert.False(s[i].pose.SameAngles(s[i - 1].pose), $"wind-up frame {s[i].frame + 1}: stopped before the top");
        // The strike moves on every hit frame...
        for (int i = strike; i < recovery; i++)
            Assert.False(s[i].pose.SameAngles(s[i - 1].pose), $"hit frame {i - strike + 1}: stopped");
        // ...then is held still for the first part of the recovery...
        var finish = s[recovery - 1].pose;
        int held = recovery;
        while (held < s.Count && s[held].pose.SameAngles(finish)) held++;
        int recoveryFrames = s.Count - recovery;
        Assert.InRange(held - recovery, recoveryFrames / 3, recoveryFrames / 2);
        // ...and the body moves on every frame back to the stance.
        for (int i = held; i < s.Count; i++)
            Assert.False(s[i].pose.SameAngles(s[i - 1].pose), $"recovery frame {s[i].frame}: stopped");
        Assert.Equal(FighterState.Recovery, s[s.Count - 1].state);
    }

    [Fact]
    public void Tiesha_zhang_raises_the_palm_overhead_in_a_horse_stance_then_strikes_forward_drawing_the_left_hand_back()
    {
        var s = PlayString("tiesha_zhang", 1);
        int strike = FirstIndex(s, 1, FighterState.Active), recovery = FirstIndex(s, 1, FighterState.Recovery);
        // The last wind-up frame: side on (the right side towards the opponent) in a wide horse stance, both knees bent
        // and out, the right arm raised up past level and bent, the hand over the head.
        var raised = s[strike - 1].pose;
        Assert.True(raised.Yaw(Joint.Hips) >= 45, "the right side should turn towards the opponent");
        foreach (var (leg, knee) in new[] { (Joint.LegR, Joint.KneeR), (Joint.LegL, Joint.KneeL) })
            Assert.True(raised.Roll(leg) >= 20 && raised.Pitch(knee) >= 60, $"{leg}: should be out wide with the knee well bent");
        Assert.True(raised.Roll(Joint.ArmR) >= 120 && raised.Pitch(Joint.ElbowR) >= 60, "the right hand should be raised over the head");
        // The hit: the palm comes down, arm all but straight, to chest height by the last active frame, the left hand drawn
        // back to the waist, the horse stance kept.
        var hit = s[strike].pose;
        var finish = s[recovery - 1].pose;
        for (int i = strike; i < recovery; i++)
            Assert.True(s[i].pose.Pitch(Joint.ElbowR) <= 20, $"active frame {i - strike + 1}: the elbow bends {s[i].pose.Pitch(Joint.ElbowR)}");
        Assert.True(finish.Pitch(Joint.ArmR) < hit.Pitch(Joint.ArmR), "the palm should come on down through the active frames");
        Assert.True(finish.Pitch(Joint.ArmL) < 0 && finish.Pitch(Joint.ElbowL) >= 45, "the left hand should be drawn back to the waist");
        Assert.Equal(raised.Pitch(Joint.KneeR), finish.Pitch(Joint.KneeR));
        Assert.Equal(raised.Pitch(Joint.KneeL), finish.Pitch(Joint.KneeL));
    }
}
