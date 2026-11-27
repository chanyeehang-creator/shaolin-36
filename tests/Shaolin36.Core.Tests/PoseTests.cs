using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Poses;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The mannequin's poses: the sheet, timing the key poses to the frame data, and the wind-up cues.</summary>
public class PoseTests
{
    private static List<Technique> SheetTechniques() =>
        TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;

    private static PoseSheet RealPoses()
    {
        var r = PoseCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("poses.csv")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!;
    }

    private const string Header = "pose,joint,pitch,yaw,roll\n";

    /// <summary>A small sheet: every base pose (each with its own chest pitch), plus 外功 wind-up and strike.</summary>
    private static PoseSheet Small(string extra = "")
    {
        var text = Header;
        int n = 1;
        foreach (var name in PoseSheet.BasePoses) text += $"{name},chest,{n++},0,0\n";
        text += "waigong.windup,arm_r,-40,0,10\nwaigong.strike,arm_r,90,0,-8\n" + extra;
        var r = PoseCsv.Parse(text);
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!;
    }

    [Fact]
    public void The_game_pose_sheet_passes_the_check_and_covers_every_technique()
    {
        var sheet = RealPoses();
        var errors = sheet.Check(SheetTechniques());
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void Each_hit_of_the_luohan_string_has_its_own_strike_pose()
    {
        var sheet = RealPoses();
        var byId = SheetTechniques().ToDictionary(t => t.Id);
        var strikes = new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" }.Select(id => sheet.StrikeOf(byId[id])).ToList();
        for (int i = 0; i < strikes.Count; i++)
            for (int j = i + 1; j < strikes.Count; j++)
                Assert.False(strikes[i].SameAngles(strikes[j]), $"hits {i + 1} and {j + 1} look the same");
    }

    [Fact]
    public void A_move_pose_named_after_the_technique_beats_the_category_default()
    {
        var sheet = Small("special.strike,arm_r,150,0,0\n");
        var special = Make.Move("special");
        var plain = Make.Move("plain");
        Assert.Equal(150, sheet.StrikeOf(special).Pitch(Joint.ArmR));
        Assert.Equal(90, sheet.StrikeOf(plain).Pitch(Joint.ArmR));
        Assert.Equal(-40, sheet.WindUpOf(special).Pitch(Joint.ArmR)); // no special wind-up: the category's
    }

    [Theory]
    [InlineData("stance,elbow,10,0,0", "unknown joint 'elbow'")]
    [InlineData("stance,arm_r,181,0,0", "'pitch' must be a whole number of degrees from -180 to 180")]
    [InlineData("stance,arm_r,ten,0,0", "'pitch' must be a whole number")]
    [InlineData(",arm_r,10,0,0", "'pose' is empty")]
    [InlineData("stance,arm_r,10,0,0\nstance,arm_r,20,0,0", "sets 'arm_r' twice")]
    public void Bad_rows_are_reported_with_their_line(string rows, string expected)
    {
        var r = PoseCsv.Parse(Header + rows + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.StartsWith("line ") && e.Contains(expected));
    }

    [Fact]
    public void A_missing_column_is_reported()
    {
        var r = PoseCsv.Parse("pose,joint,pitch,yaw\nstance,hips,1,2\n");
        Assert.Contains(r.Errors, e => e.Contains("missing column 'roll'"));
    }

    [Fact]
    public void Empty_angles_and_left_out_joints_are_0()
    {
        var r = PoseCsv.Parse(Header + "stance,arm_r,30,,\n");
        Assert.True(r.Ok);
        var p = r.Sheet!.Stance;
        Assert.Equal(30, p.Pitch(Joint.ArmR));
        Assert.Equal(0, p.Yaw(Joint.ArmR));
        Assert.Equal(0, p.Pitch(Joint.KneeL));
    }

    [Fact]
    public void The_check_finds_missing_poses_and_typos()
    {
        var r = PoseCsv.Parse(Header + "stance,hips,0,0,0\nwaigong.windup,arm_r,1,0,0\nwaigong.strike,arm_r,1,0,0\nluohan_quan_9.strike,arm_r,1,0,0\nwaigong.hit,arm_r,1,0,0\nwave,arm_r,1,0,0\n");
        Assert.True(r.Ok);
        var errors = r.Sheet!.Check(new[] { Make.Move("punch"), Make.Move("palm", category: Category.Neigong) });
        Assert.Contains(errors, e => e.Contains("missing pose 'hitstun'"));
        Assert.Contains(errors, e => e.Contains("'palm' has no wind-up pose"));
        Assert.Contains(errors, e => e.Contains("'palm' has no strike pose"));
        Assert.Contains(errors, e => e.Contains("no technique or category called 'luohan_quan_9'"));
        Assert.Contains(errors, e => e.Contains("'waigong.hit' must end in .windup or .strike"));
        Assert.Contains(errors, e => e.Contains("pose 'wave' is not used"));
        Assert.DoesNotContain(errors, e => e.Contains("'punch'"));
    }

    [Fact]
    public void Blend_goes_from_one_pose_to_the_other_in_whole_degrees()
    {
        var sheet = Small();
        var a = sheet.Find("waigong.windup")!; // arm_r pitch -40, roll 10
        var b = sheet.Find("waigong.strike")!; // arm_r pitch 90, roll -8
        Assert.Same(a, Pose.Blend(a, b, 0));
        Assert.Same(b, Pose.Blend(a, b, 1000));
        var mid = Pose.Blend(a, b, 500);
        Assert.Equal(25, mid.Pitch(Joint.ArmR));
        Assert.Equal(1, mid.Roll(Joint.ArmR));
        // Rounds the same way up and down.
        Assert.Equal(-Pose.Blend(b, a, 250).Roll(Joint.ArmR), Pose.Blend(Negate(b), Negate(a), 250).Roll(Joint.ArmR));
    }

    private static Pose Negate(Pose p)
    {
        var r = PoseCsv.Parse(Header + $"x,arm_r,{-p.Pitch(Joint.ArmR)},{-p.Yaw(Joint.ArmR)},{-p.Roll(Joint.ArmR)}\n");
        return r.Sheet!.Find("x")!;
    }

    [Fact]
    public void Ease_starts_slow_ends_slow_and_never_goes_backward()
    {
        Assert.Equal(0, Pose.Ease(0));
        Assert.Equal(500, Pose.Ease(500));
        Assert.Equal(1000, Pose.Ease(1000));
        Assert.True(Pose.Ease(100) < 100);
        Assert.True(Pose.Ease(900) > 900);
        for (int x = 1; x <= 1000; x++) Assert.True(Pose.Ease(x) >= Pose.Ease(x - 1));
    }

    [Theory]
    [InlineData(13, 3, 21)] // 罗汉拳 1
    [InlineData(36, 4, 36)] // 铁砂掌
    [InlineData(1, 1, 8)]   // 硬气功: guards from frame 0
    [InlineData(2, 17, 10)] // 化绵掌
    [InlineData(3, 2, 6)]   // 无相劫指
    public void The_strike_pose_shows_exactly_on_the_active_frames(int startup, int active, int recovery)
    {
        var t = Make.Move(startup: startup, active: active, recovery: recovery);
        int reached = MoveTimeline.WindUpReachedAfter(t);
        Assert.Equal(System.Math.Max(1, (startup + 1) / 2), reached);
        for (int frame = 0; frame < t.TotalFrames; frame++)
        {
            var step = MoveTimeline.At(t, frame);
            bool strikePose = step.From == PoseKey.Strike && step.To == PoseKey.Strike;
            Assert.Equal(t.IsActive(frame), strikePose);
            if (frame < startup)
            {
                Assert.Equal(PoseKey.WindUp, step.To);
                // Halfway through the startup the wind-up is reached and held, cocked, up to the strike.
                if (frame + 1 >= reached) Assert.Equal(1000, step.Permille);
                else Assert.InRange(step.Permille, 0, 999);
            }
            if (frame >= startup + active) Assert.Equal(PoseKey.Stance, step.To);
        }
        Assert.Equal(1000, MoveTimeline.At(t, t.TotalFrames - 1).Permille); // back in the stance on the last frame
    }

    [Fact]
    public void A_fighter_winds_up_strikes_on_its_first_active_frame_and_settles_back_into_the_stance()
    {
        var sheet = Small();
        var punch = Make.Move("punch", startup: 10, active: 3, recovery: 12);
        var f = new Fighter();
        f.Loadout.Assign(Slot.Square, punch);
        Assert.True(sheet.PoseAt(f).SameAngles(sheet.Stance));

        f.Tick(SlotInput.Press(Slot.Square));
        bool sawStrike = false;
        for (int n = 0; n < 40 && !f.CanAct; n++)
        {
            var pose = sheet.PoseAt(f);
            if (f.State == FighterState.Startup && f.MoveFrame + 1 >= MoveTimeline.WindUpReachedAfter(punch))
                Assert.True(pose.SameAngles(sheet.WindUpOf(punch)), $"frame {f.MoveFrame}: not in the wind-up");
            if (f.State == FighterState.Active)
            {
                Assert.True(pose.SameAngles(sheet.StrikeOf(punch)), $"frame {f.MoveFrame}: not striking");
                sawStrike = true;
            }
            f.Tick(SlotInput.None);
        }
        Assert.True(sawStrike);
        Assert.True(sheet.PoseAt(f).SameAngles(sheet.Stance));
    }

    [Fact]
    public void A_string_hit_winds_up_from_the_pose_it_starts_in()
    {
        var sheet = Small();
        var punch = Make.Move("punch", startup: 10, active: 3, recovery: 12);
        var strike = sheet.StrikeOf(punch);
        // Frame 0 of the startup is barely off the entry pose: from the last hit's strike, not the stance.
        var fromStrike = sheet.MovePose(punch, 0, strike);
        var fromStance = sheet.MovePose(punch, 0);
        Assert.True(System.Math.Abs(fromStrike.Pitch(Joint.ArmR) - strike.Pitch(Joint.ArmR)) < 20);
        Assert.False(fromStrike.SameAngles(fromStance));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(FootLift.StartDegrees, 0)]
    [InlineData(80, 500)]
    [InlineData(FootLift.FullDegrees, 1000)]
    [InlineData(110, 1000)]
    public void A_leg_leaves_the_floor_between_75_and_85_degrees(int degrees, int expected)
    {
        Assert.Equal(expected, FootLift.OfRaise(degrees));
    }

    private static Pose Legs(int right, int left, int rightRoll = 0)
    {
        var r = PoseCsv.Parse(Header + $"legs,leg_r,{right},0,{rightRoll}\nlegs,leg_l,{left},0,0\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!.Find("legs")!;
    }

    [Fact]
    public void The_raised_leg_lifts_whether_it_swings_forward_back_or_out()
    {
        Assert.Equal(1000, FootLift.Of(Legs(0, 110), out bool right));
        Assert.False(right);
        Assert.Equal(1000, FootLift.Of(Legs(-90, 0), out right));
        Assert.True(right);
        Assert.Equal(1000, FootLift.Of(Legs(0, 0, rightRoll: 85), out right));
        Assert.True(right);
    }

    [Fact]
    public void Both_legs_raised_the_same_stand_on_both_feet()
    {
        Assert.Equal(0, FootLift.Of(Legs(90, 90), out _));
        Assert.Equal(500, FootLift.Of(Legs(90, 80), out _));
    }

    [Fact]
    public void Every_stance_in_the_game_stands_on_both_feet_and_the_luohan_kick_lifts_the_right_foot()
    {
        var sheet = RealPoses();
        // Only knee raises, kicks and 虎形拳's pounce (its back leg flies up behind) lift a foot (纵云梯's jump is in the air,
        // so its feet are never planted).
        var lifted = new[]
        {
            "luohan_quan_1.windup", "luohan_quan_3.windup", "luohan_quan_3.strike", "huxing_quan_1.strike", "huxing_quan_3.windup",
            "zongyun_ti.strike",
        };
        foreach (var name in sheet.Names)
        {
            if (System.Array.IndexOf(lifted, name) >= 0) continue;
            Assert.True(FootLift.Of(sheet.Find(name)!, out _) == 0, $"{name} lifts a foot");
        }
        Assert.Equal(1000, FootLift.Of(sheet.Find("luohan_quan_3.strike")!, out bool right));
        Assert.True(right);
    }

    [Fact]
    public void Winding_up_a_kick_lifts_the_foot_gradually()
    {
        var sheet = RealPoses();
        var kick = SheetTechniques().Single(t => t.Id == "luohan_quan_3");
        int last = 0;
        for (int frame = 0; frame < kick.StartupFrames; frame++)
        {
            int lift = FootLift.Of(sheet.MovePose(kick, frame), out _);
            Assert.True(lift >= last, $"frame {frame}: the foot comes back down");
            last = lift;
        }
        Assert.Equal(1000, last);
        Assert.Equal(1000, FootLift.Of(sheet.MovePose(kick, kick.StartupFrames), out _));
    }

    [Fact]
    public void Hitstun_and_stagger_have_their_own_poses()
    {
        var sheet = Small();
        var f = new Fighter();
        f.Stun(FighterState.Hitstun, 10);
        Assert.Same(sheet.Find("hitstun"), sheet.PoseAt(f));
        f = new Fighter();
        f.Stun(FighterState.Stagger, 10);
        Assert.Same(sheet.Find("stagger"), sheet.PoseAt(f));
    }

    [Fact]
    public void Wind_up_cues_follow_the_weight_and_grabs_flash()
    {
        var byId = SheetTechniques().ToDictionary(t => t.Id);
        Assert.Equal(WindUpCue.Medium, WindUpCues.Of(byId["luohan_quan_1"]));
        Assert.Equal(WindUpCue.Light, WindUpCues.Of(byId["luohan_quan_2"]));
        Assert.Equal(WindUpCue.Heavy, WindUpCues.Of(byId["tiesha_zhang"]));
        Assert.Equal(WindUpCue.Energy, WindUpCues.Of(byId["shihou_gong"]));
        Assert.Equal(WindUpCue.Light, WindUpCues.Of(byId["fengxue"]));
        Assert.Equal(WindUpCue.Grab, WindUpCues.Of(byId["qinna_shou"]));
        Assert.Equal(WindUpCue.Grab, WindUpCues.Of(byId["longzhua_shou"]));
        // Not attacks: no cue.
        foreach (var id in new[] { "timed_parry", "yingqi_gong", "qinggong_dash", "zongyun_ti", "xianliao_shu", "yijinjing" })
            Assert.Equal(WindUpCue.None, WindUpCues.Of(byId[id]));
        Assert.True(WindUpCues.Flashes(WindUpCue.Grab));
        foreach (var cue in new[] { WindUpCue.None, WindUpCue.Light, WindUpCue.Medium, WindUpCue.Heavy, WindUpCue.Energy })
            Assert.False(WindUpCues.Flashes(cue));
    }

    [Fact]
    public void A_fighter_shows_its_cue_only_in_the_wind_up_and_it_builds_to_the_strike()
    {
        var punch = Make.Move("punch", Weight.Heavy, startup: 10, active: 3, recovery: 12);
        var f = new Fighter();
        f.Loadout.Assign(Slot.Square, punch);
        Assert.Equal(WindUpCue.None, WindUpCues.Of(f));
        f.Tick(SlotInput.Press(Slot.Square));
        int last = -1;
        while (f.State == FighterState.Startup)
        {
            Assert.Equal(WindUpCue.Heavy, WindUpCues.Of(f));
            int p = WindUpCues.Progress(f);
            Assert.True(p > last);
            last = p;
            f.Tick(SlotInput.None);
        }
        Assert.Equal(1000, last); // full on the last wind-up frame
        Assert.Equal(FighterState.Active, f.State);
        Assert.Equal(WindUpCue.None, WindUpCues.Of(f));
        Assert.Equal(0, WindUpCues.Progress(f));
    }
}
