using System.IO;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Chan: you can quick-step out of a combo, like the 轻功 dash escape, but not out of breath. Ordinary opponents never do; a mini
/// boss or the boss sometimes does (<see cref="Brain.StepEscapePct"/>), and only when your combo caught it doing nothing: never
/// during its own attack or string, or in its opening just after one. Out of breath you still walk at full speed; the opponent slows.
/// </summary>
public class StepEscapeTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Loadout Set(bool dash = false)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.Triangle, Book.Find("tiesha_zhang")!);
        l.Assign(Slot.L1, Book.Find("timed_parry")!);
        if (dash) l.Assign(Slot.Cross, Book.Find("qinggong_dash")!);
        return l;
    }

    // ---------- You ----------

    /// <summary>Puts One in hitstun from Two's 罗汉拳 hit 1 (from 100 cm) and returns the duel on the tick after the hit.</summary>
    private static Duel OneInHitstun(bool favoured = true)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 100);
        if (favoured) d.FavourOne(false);
        d.Step(SlotInput.None, SlotInput.Press(Slot.Square));
        for (int guard = 0; guard < 300 && d.One.State != FighterState.Hitstun; guard++) d.Step(SlotInput.None, SlotInput.None);
        d.Step(SlotInput.None, SlotInput.None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        return d;
    }

    private static void StepBack(Duel d) => d.Step(SlotInput.None, SlotInput.None, MoveInput.Step(StepDirection.Back), MoveInput.None);

    [Fact]
    public void You_can_quick_step_out_of_hitstun()
    {
        var d = OneInHitstun();
        int stamina = d.One.Stamina;
        StepBack(d);
        Assert.Equal(FighterState.QuickStep, d.One.State);
        Assert.True(d.One.EscapedHitstun, "a combo escape: no just dodge from it");
        Assert.Equal(stamina - d.One.QuickStepRules!.Stamina, d.One.Stamina);
        Assert.True(d.One.IsInvincible, "the slide is invincible as usual");
    }

    [Fact]
    public void Without_the_edge_a_quick_step_waits_for_the_hitstun_to_end()
    {
        var d = OneInHitstun(favoured: false);
        StepBack(d);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void Out_of_breath_you_cant_quick_step_out_of_hitstun()
    {
        var d = OneInHitstun();
        d.One.LoseStamina(d.One.Stamina);
        Assert.True(d.One.IsExhausted);
        StepBack(d);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void Locked_by_wuxiang_jiezhi_you_cant_quick_step_out_of_hitstun()
    {
        var d = OneInHitstun();
        d.One.Cripple(300);
        StepBack(d);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void A_normal_quick_step_isnt_an_escape()
    {
        var d = new Duel(Rules, Set(), Set(), 0, 200);
        d.FavourOne(false);
        StepBack(d);
        Assert.Equal(FighterState.QuickStep, d.One.State);
        Assert.False(d.One.EscapedHitstun);
    }

    [Fact]
    public void Out_of_breath_you_still_walk_at_full_speed_but_the_opponent_slows()
    {
        var d = new Duel(Rules, Set(), Set(), 0, 400);
        d.FavourOne(false);
        d.One.LoseStamina(d.One.Stamina);
        d.Two.LoseStamina(d.Two.Stamina);
        for (int i = 0; i < 60; i++) d.Step(SlotInput.None, SlotInput.None, MoveInput.WalkForward, MoveInput.WalkForward);
        Assert.True(d.One.IsExhausted && d.Two.IsExhausted, "both still out of breath");
        Assert.InRange(d.One.XCm, Rules.WalkSpeed - 1, Rules.WalkSpeed + 1);
        int slow = Rules.WalkSpeed * Rules.ExhaustedWalkPct / 100;
        Assert.True(slow < Rules.WalkSpeed);
        Assert.InRange(400 - d.Two.XCm, slow - 1, slow + 1);
    }

    // ---------- The opponent brain ----------

    public enum Caught { DoingNothing, Attacking, InItsOpening }

    /// <summary>Notices at once (reaction 0); never attacks, parries, clashes or steps on its own.</summary>
    private static readonly BrainStyle Still = new("test", "test", 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// You land 罗汉拳 hit 1 on the brain (Two). <paramref name="caught"/>: while it stands there (from 100 cm); while its 铁砂掌
    /// winds up (it presses first, you hit it early in the wind-up); or right after its own 罗汉拳 whiffs from 200 cm (you quick
    /// step in once it's free, then punch). True if it got out of the combo (a quick step, or a dash with <paramref name="dash"/>).
    /// It notices <paramref name="reaction"/> frames late.
    /// </summary>
    private static bool GetsOut(Caught caught, int stepEscapePct, int opening = 0, bool dash = false, int step = 0, int reaction = 0)
    {
        var d = new Duel(Rules, Set(), Set(dash), 0, caught == Caught.InItsOpening ? 200 : 100);
        d.FavourOne(false);
        var style = new BrainStyle("test", "test", reaction, 10, 0, 0, 0, 0, 0, step, 0, 0, 0);
        var brain = new Brain(style, 3) { StepEscapePct = stepEscapePct, OpeningFrames = opening };
        bool pressed = false, stepped = false, gotOut = false;
        int landedAt = -1;
        for (int i = 0; i < 400 && !d.IsOver; i++)
        {
            var b = brain.Think(d, Side.Two);
            var two = b.Slots;
            if (i == 0 && caught == Caught.Attacking) two = SlotInput.Press(Slot.Triangle);
            if (i == 0 && caught == Caught.InItsOpening) two = SlotInput.Press(Slot.Square);
            var one = SlotInput.None;
            var oneMove = MoveInput.None;
            if (!pressed)
            {
                if (caught == Caught.DoingNothing && i == 0 || caught == Caught.Attacking && i == 1)
                {
                    one = SlotInput.Press(Slot.Square);
                    pressed = true;
                }
                else if (caught == Caught.InItsOpening && i > 0 && d.Two.CanAct && d.One.CanAct)
                {
                    if (!stepped) oneMove = MoveInput.Step(StepDirection.Forward);
                    else one = SlotInput.Press(Slot.Square);
                    pressed = stepped;
                    stepped = true;
                }
            }
            d.Step(one, two, oneMove, b.Move);
            if (d.Two.EscapedHitstun) gotOut = true;
            if (landedAt < 0 && d.Hits.Any(h => h.Attacker == Side.One && h.Landed)) landedAt = i;
            if (landedAt >= 0 && i > landedAt + 60) break;
        }
        Assert.True(landedAt >= 0, "your hit landed");
        return gotOut;
    }

    [Fact]
    public void A_mini_boss_or_boss_caught_doing_nothing_steps_out_of_your_combo()
    {
        Assert.True(GetsOut(Caught.DoingNothing, stepEscapePct: 100));
    }

    [Fact]
    public void An_ordinary_opponent_never_steps_out()
    {
        Assert.False(GetsOut(Caught.DoingNothing, stepEscapePct: 0));
    }

    [Fact]
    public void Hit_during_its_own_attack_it_takes_the_combo()
    {
        Assert.False(GetsOut(Caught.Attacking, stepEscapePct: 100));
    }

    [Fact]
    public void Hit_in_its_opening_after_an_attack_it_takes_the_combo()
    {
        Assert.True(GetsOut(Caught.InItsOpening, stepEscapePct: 100, opening: 0), "no opening: it was free again");
        Assert.False(GetsOut(Caught.InItsOpening, stepEscapePct: 100, opening: 60));
    }

    [Fact]
    public void Its_dash_escape_also_needs_the_combo_to_catch_it_doing_nothing()
    {
        // Slow to notice (20 frames), so your punch lands before its step chance can take it aside from the punch.
        Assert.True(GetsOut(Caught.DoingNothing, stepEscapePct: 0, dash: true, step: 100, reaction: 20));
        Assert.False(GetsOut(Caught.Attacking, stepEscapePct: 0, dash: true, step: 100, reaction: 20));
    }

    [Fact]
    public void The_brain_says_it_stepped_out()
    {
        var d = new Duel(Rules, Set(), Set(), 0, 100);
        d.FavourOne(false);
        var brain = new Brain(Still, 3) { StepEscapePct = 100 };
        bool said = false;
        for (int i = 0; i < 60; i++)
        {
            var b = brain.Think(d, Side.Two);
            said |= brain.Intent == BrainIntent.SteppingOut;
            d.Step(i == 0 ? SlotInput.Press(Slot.Square) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
        }
        Assert.True(said);
    }
}
