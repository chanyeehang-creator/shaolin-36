using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// 擒拿手: a fast, short grab that beats a parry. It catches, holds for a moment, then throws.
/// Strike beats grab, grab beats parry, parry beats strike. Played on the real sheets.
/// </summary>
public class GrabTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Technique T(string id) => Book.Find(id)!;
    private static Technique Grab => T("qinna_shou");

    private static readonly SlotInput None = SlotInput.None;
    private static SlotInput P(Slot s) => SlotInput.Press(s);

    /// <summary>The playtest loadout: 罗汉拳 □, 铁砂掌 △, 封穴 ○, dash ✕, 化绵掌 L1, 仙辽术 R1, 擒拿手 R2.</summary>
    private static Loadout Full()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("luohan_quan_1"));
        l.Assign(Slot.Triangle, T("tiesha_zhang"));
        l.Assign(Slot.Circle, T("fengxue"));
        l.Assign(Slot.Cross, T("qinggong_dash"));
        l.Assign(Slot.L1, T("timed_parry"));
        l.Assign(Slot.R1, T("xianliao_shu"));
        l.Assign(Slot.R2, Grab);
        return l;
    }

    private static Duel DuelAt(int apart, int? twoQi = null) => new(Rules, Full(), Full(), 0, apart, null, twoQi);

    /// <summary>Step until tick <paramref name="tick"/> has run, pressing as given on chosen ticks; returns every event.</summary>
    private static List<HitEvent> RunTo(Duel d, int tick, Func<int, SlotInput>? one = null, Func<int, SlotInput>? two = null)
    {
        var all = new List<HitEvent>();
        while (d.Tick <= tick) { int t = d.Tick; all.AddRange(d.Step(one?.Invoke(t) ?? None, two?.Invoke(t) ?? None)); }
        return all;
    }

    private static Func<int, SlotInput> At(int tick, Slot slot) => t => t == tick ? P(slot) : None;

    // ---------- The sheet ----------

    [Fact]
    public void Qinna_shou_is_a_fast_short_unclashable_grab_with_no_cooldown()
    {
        Assert.Equal("小擒拿手 (grab)", Grab.Name);
        Assert.Equal(Category.Qinna, Grab.Category);
        Assert.True(Grab.IsGrab);
        Assert.False(Grab.Clashable);
        Assert.True(Grab.StartupFrames < T("luohan_quan_1").StartupFrames, "faster to reach in than a 罗汉拳 hit");
        Assert.True(Grab.FullReachCm < T("luohan_quan_1").FullReachCm, "shorter than a 罗汉拳 hit: you have to be close");
        Assert.True(Grab.RecoveryFrames >= 30, "a whiff is a sure punish");
        Assert.Equal(30, Grab.HoldFrames);
        Assert.True(Grab.KnockbackCm > 0 && Grab.Knockdown, "the throw sends them away, down on the floor");
        Assert.False(Grab.HitsDown);
    }

    // ---------- Catch, hold, throw ----------

    [Fact]
    public void A_grab_catches_holds_then_throws()
    {
        var d = DuelAt(100);
        var hits = RunTo(d, 10, one: At(0, Slot.R2));

        var caught = Assert.Single(hits);
        Assert.Equal(GrabPhase.Caught, caught.Grab);
        Assert.Equal(10, caught.Tick); // the first active frame
        Assert.False(caught.Landed);
        Assert.Equal(0, caught.Damage);
        Assert.Equal(FighterState.Grabbed, d.Two.State);
        Assert.True(d.One.IsHolding);

        // Held for 30 frames: nobody moves on, nothing lands.
        var during = RunTo(d, 39);
        Assert.Empty(during);
        Assert.Equal(FighterState.Grabbed, d.Two.State);
        Assert.Equal(10, d.One.MoveFrame); // paused on the frame it caught
        Assert.Equal(Rules.HpMax, d.Two.Hp);

        // Tick 40: the hold runs out and the throw lands.
        var thrown = Assert.Single(d.Step(None, None));
        Assert.Equal(GrabPhase.Thrown, thrown.Grab);
        Assert.Equal(40, thrown.Tick);
        Assert.True(thrown.Landed);
        Assert.Equal(Grab.Damage, thrown.Damage);
        Assert.Equal(Rules.HpMax - Grab.Damage, d.Two.Hp);
        Assert.Equal(Grab.KnockbackCm, thrown.KnockbackCm);
        Assert.Equal(Rules.QiPerHit, thrown.AttackerQiGained);
        Assert.True(thrown.KnockedDown);
        Assert.Equal(FighterState.KnockedDown, d.Two.State);
        Assert.False(d.One.IsHolding);
        d.Step(None, None);
        Assert.Equal(11, d.One.MoveFrame); // the move carries on after the throw
    }

    [Fact]
    public void After_the_throw_the_grabber_is_free_first_but_too_far_for_a_free_hit()
    {
        var d = DuelAt(100);
        RunTo(d, 40, one: At(0, Slot.R2));
        int throwTick = 40;
        int grabberFree = -1, thrownFree = -1;
        while (grabberFree < 0 || thrownFree < 0)
        {
            int t = d.Tick;
            d.Step(None, d.Two.State == FighterState.KnockedDown ? P(Slot.Square) : None);
            if (grabberFree < 0 && d.One.CanAct) grabberFree = t;
            if (thrownFree < 0 && d.Two.CanAct) thrownFree = t;
        }
        Assert.Equal(throwTick + Rules.DownMin + Rules.RiseFrames, thrownFree); // the quickest rise
        Assert.True(grabberFree < thrownFree, "the grabber recovers first");
        Assert.True(d.DistanceCm > T("luohan_quan_1").FullReachCm, $"thrown {d.DistanceCm} cm: out of reach of a 罗汉拳 hit");
    }

    [Fact]
    public void Being_held_stops_you_doing_anything_even_the_dash()
    {
        var d = DuelAt(100);
        RunTo(d, 10, one: At(0, Slot.R2));
        for (int i = 0; i < 20; i++)
        {
            d.Step(None, P(i % 2 == 0 ? Slot.Cross : Slot.Square), MoveInput.None, MoveInput.WalkBack);
            Assert.Equal(FighterState.Grabbed, d.Two.State);
        }
    }

    [Fact]
    public void A_whiffed_grab_runs_its_full_recovery()
    {
        var d = DuelAt(300);
        var hits = RunTo(d, Grab.TotalFrames - 1, one: At(0, Slot.R2));
        Assert.Empty(hits);
        Assert.Equal(FighterState.Recovery, d.One.State);
        d.Step(None, None);
        Assert.True(d.One.CanAct);
    }

    // ---------- What it beats and what beats it ----------

    [Fact]
    public void A_grab_goes_through_a_perfect_parry()
    {
        var d = DuelAt(100);
        // Parry pressed on tick 6: frame 4 of it (perfect) when the grab comes out on tick 10.
        var hits = RunTo(d, 10, one: At(0, Slot.R2), two: At(6, Slot.L1));
        var caught = Assert.Single(hits);
        Assert.Equal(GrabPhase.Caught, caught.Grab);
        Assert.Equal(ParryOutcome.NotParried, caught.Parry);
        Assert.Equal("timed_parry", caught.DefenderTechnique!.Id);
        Assert.Equal(FighterState.Grabbed, d.Two.State);
    }

    [Fact]
    public void A_grab_goes_through_hyper_armor()
    {
        var d = DuelAt(100, twoQi: 30);
        var hits = RunTo(d, 10, one: At(0, Slot.R2), two: At(0, Slot.Triangle));
        Assert.Equal(GrabPhase.Caught, Assert.Single(hits).Grab);
        Assert.Equal(FighterState.Grabbed, d.Two.State);
    }

    [Fact]
    public void A_grab_never_clashes()
    {
        var d = DuelAt(100);
        // 罗汉拳 hit 1 started on tick 0 is in its clash window (frames 8-12) when the grab catches on tick 10.
        var hits = RunTo(d, 10, one: At(0, Slot.R2), two: At(0, Slot.Square));
        var caught = Assert.Single(hits);
        Assert.Equal(ClashOutcome.NoClash, caught.Clash);
        Assert.Equal(GrabPhase.Caught, caught.Grab);
    }

    [Fact]
    public void A_strike_landing_on_the_same_tick_beats_the_grab()
    {
        var d = DuelAt(100);
        // 罗汉拳 hit 1 (13 frames) pressed on tick 0 and the grab (10 frames) on tick 3 both connect on tick 13.
        var hits = RunTo(d, 13, one: At(3, Slot.R2), two: At(0, Slot.Square));
        var hit = Assert.Single(hits);
        Assert.Equal(Side.Two, hit.Attacker);
        Assert.True(hit.Landed);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        Assert.NotEqual(FighterState.Grabbed, d.Two.State);
    }

    [Fact]
    public void Two_grabs_on_the_same_tick_both_come_to_nothing()
    {
        var d = DuelAt(100);
        var hits = RunTo(d, Grab.StartupFrames + Grab.ActiveFrames, one: At(0, Slot.R2), two: At(0, Slot.R2));
        Assert.Empty(hits);
        Assert.Equal(FighterState.Recovery, d.One.State);
        Assert.Equal(FighterState.Recovery, d.Two.State);
    }

    [Fact]
    public void A_grab_cant_catch_a_fighter_in_hitstun()
    {
        var d = DuelAt(100);
        RunTo(d, 9, one: At(0, Slot.R2));
        d.Two.TakeHit(0, 60);
        var hits = RunTo(d, Grab.StartupFrames + Grab.ActiveFrames + 2);
        Assert.Empty(hits);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        Assert.False(d.One.IsHolding);
    }

    [Fact]
    public void A_dash_through_the_grab_is_a_just_dodge()
    {
        var d = DuelAt(100);
        var hits = RunTo(d, 10, one: At(0, Slot.R2), two: At(5, Slot.Cross));
        var dodge = Assert.Single(hits);
        Assert.True(dodge.JustDodged);
        Assert.Equal(GrabPhase.None, dodge.Grab);
        Assert.NotEqual(FighterState.Grabbed, d.Two.State);
    }

    // ---------- Feel ----------

    [Fact]
    public void The_catch_and_the_throw_have_their_own_feel_rows()
    {
        var d = DuelAt(100);
        var hits = RunTo(d, 40, one: At(0, Slot.R2));
        Assert.Equal(FeelEvent.Grab, FeelCue.Classify(hits[0]));
        Assert.Equal(FeelEvent.Throw, FeelCue.Classify(hits[1]));
        var feel = FeelCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("feel.csv")));
        Assert.True(feel.Ok, string.Join("\n", feel.Errors));
        Assert.True(feel.Sheet![FeelEvent.Throw].HitStopFrames > feel.Sheet[FeelEvent.Grab].HitStopFrames, "the throw is the big beat");
    }

    // ---------- The sheet's checks ----------

    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry,lunge,hold\n";

    [Theory]
    [InlineData("x,x,waigong,light,false,10,4,30,0,0,15,0,14,false,50,100,,0,0,0,30", "only 擒拿")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,50,100,,0,0,0,0", "needs 'hold'")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,50,0,,0,0,0,30", "never catches")]
    [InlineData("x,x,qinna,light,false,10,4,30,2,0,15,0,14,true,50,100,,0,0,0,30", "can't be clashed")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,0,100,,0,0,0,30", "'hitstun' 1 or more")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,50,100,,0,0,50,30", "'lunge'")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,50,100,,0,0,0,9999", "frames or less")]
    public void Bad_grab_cells_are_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    // ---------- The opponent brain ----------

    private static BrainStyle Style(int grab = 0, int parry = 0, int aggression = 0) =>
        new("t", "t", reaction: 0, thinkEvery: 1, aggression: aggression, punish: 0, chain: 0, parry: parry, perfect: 0, step: 0,
            circle: 0, restBelow: 0, heavy: 0, guess: 0, heal: 0, grab: grab);

    [Fact]
    public void The_brain_grabs_when_its_grab_reaches()
    {
        var d = DuelAt(100);
        var brain = new Brain(Style(grab: 100), 1);
        for (int i = 0; i < 5 && d.Two.Technique == null; i++) d.Step(None, brain.Think(d, Side.Two).Slots);
        Assert.Equal("qinna_shou", d.Two.Technique?.Id);
        Assert.Equal(BrainIntent.Grabbing, brain.Intent);
    }

    [Fact]
    public void The_brain_doesnt_grab_out_of_reach_or_into_hitstun()
    {
        var far = DuelAt(250);
        var brain = new Brain(Style(grab: 100), 1);
        for (int i = 0; i < 30; i++)
        {
            far.Step(None, brain.Think(far, Side.Two).Slots, MoveInput.WalkBack, MoveInput.None);
            Assert.NotEqual("qinna_shou", far.Two.Technique?.Id);
        }

        var near = DuelAt(100);
        near.One.TakeHit(0, 30);
        var brain2 = new Brain(Style(grab: 100), 1);
        for (int i = 0; i < 20; i++)
        {
            var press = brain2.Think(near, Side.Two).Slots;
            bool stunned = near.One.State == FighterState.Hitstun;
            near.Step(None, press);
            if (stunned) Assert.NotEqual("qinna_shou", near.Two.Technique?.Id);
        }
    }

    [Fact]
    public void The_brain_never_parries_a_grab_it_sees_coming()
    {
        var d = DuelAt(100);
        var brain = new Brain(Style(parry: 100), 1);
        for (int i = 0; i < 12; i++)
        {
            d.Step(i == 0 ? P(Slot.R2) : None, brain.Think(d, Side.Two).Slots);
            Assert.NotEqual(Category.Fangshen, d.Two.Technique?.Category);
        }
        Assert.Equal(FighterState.Grabbed, d.Two.State);
    }

    [Fact]
    public void The_brain_still_parries_a_strike()
    {
        var d = DuelAt(100);
        var brain = new Brain(Style(parry: 100), 1);
        bool parried = false;
        for (int i = 0; i < 14; i++)
        {
            d.Step(i == 0 ? P(Slot.Square) : None, brain.Think(d, Side.Two).Slots);
            parried |= d.Two.Technique?.Category == Category.Fangshen;
        }
        Assert.True(parried);
    }
}
