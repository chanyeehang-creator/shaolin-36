using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// The default 轻功 dash on ✕ (Stage 3): longer than the quick step, invincible on startup, costs more stamina.
/// It goes the way the stick is held when pressed, or straight at the opponent with no direction held.
/// Played on the real technique and rules sheets.
/// </summary>
public class DashTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);
    private static Technique Dash => Book.Find("qinggong_dash")!;

    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Cross = SlotInput.Press(Slot.Cross);
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    /// <summary>The playtest loadout: 罗汉拳 on □, 铁砂掌 on △, 轻功 dash on ✕, 化绵掌 on L1.</summary>
    private static Loadout Starter()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.Triangle, Book.Find("tiesha_zhang")!);
        l.Assign(Slot.Cross, Dash);
        l.Assign(Slot.L1, Book.Find("timed_parry")!);
        return l;
    }

    private static Duel DuelAt(int apart) => new(Rules, Starter(), Starter(), 0, apart);

    /// <summary>One presses ✕ on tick 0 with <paramref name="stick"/> held, then the dash plays out.</summary>
    private static Duel DashFrom(int apart, MoveInput stick)
    {
        var d = DuelAt(apart);
        d.Step(Cross, None, stick, MoveInput.None);
        for (int t = 1; t < Dash.TotalFrames; t++) d.Step(None, None);
        return d;
    }

    // ---------- The sheet ----------

    [Fact]
    public void The_sheet_dash_goes_farther_than_a_quick_step_costs_more_and_is_invincible_from_the_press()
    {
        Assert.Equal(Category.Qinggong, Dash.Category);
        Assert.True(Dash.DashCm > Math.Max(Rules.QuickStepDistance, Rules.QuickStepSideDistance));
        Assert.True(Dash.StaminaCost > Rules.QuickStepStamina);
        Assert.True(Dash.IsInvincible(0));
        Assert.True(Dash.InvincibleFrames >= 6);
        Assert.Equal(0, Dash.ReachCm); // it never strikes
    }

    // ---------- Direction ----------

    [Fact]
    public void With_no_direction_it_dashes_straight_at_the_opponent()
    {
        var d = DashFrom(400, MoveInput.None);
        Assert.Equal(400 - Dash.DashCm, d.DistanceCm);
        Assert.Equal(Dash.DashCm, d.One.XCm);
        Assert.Equal(0, d.One.ZCm);
    }

    [Fact]
    public void With_no_direction_it_closes_in_to_touching_range_and_stops()
    {
        var d = DashFrom(Dash.DashCm + 50, MoveInput.None);
        Assert.Equal(Rules.MinDistance, d.DistanceCm);
        Assert.Equal(Dash.DashCm + 50, d.Two.XCm); // the opponent isn't pushed
    }

    [Fact]
    public void From_the_duel_start_one_dash_in_puts_you_in_luohan_quan_reach()
    {
        var d = DashFrom(Rules.StartDistance, MoveInput.None);
        Assert.InRange(d.DistanceCm, Rules.MinDistance, Book.Find("luohan_quan_1")!.ReachCm);
    }

    [Fact]
    public void Held_back_it_dashes_away()
    {
        var d = DashFrom(200, MoveInput.WalkBack);
        Assert.Equal(-Dash.DashCm, d.One.XCm);
        Assert.Equal(200 + Dash.DashCm, d.DistanceCm);
    }

    [Fact]
    public void Held_to_the_side_it_curves_round_the_opponent()
    {
        var d = DashFrom(200, MoveInput.WalkRight);
        Assert.True(d.One.ZCm > 100, $"went {d.One.ZCm} cm to the side");
        Assert.InRange(d.DistanceCm, 200, 300);
    }

    [Fact]
    public void The_slide_is_its_startup_and_active_frames_then_it_recovers_in_place()
    {
        var d = DuelAt(600);
        d.Step(Cross, None, MoveInput.WalkBack, MoveInput.None);
        for (int t = 1; t < Dash.DashFrames; t++) d.Step(None, None);
        int slid = d.One.XCm;
        Assert.Equal(-Dash.DashCm, slid);
        for (int t = Dash.DashFrames; t < Dash.TotalFrames; t++)
        {
            d.Step(None, None);
            Assert.Equal(slid, d.One.XCm);
            Assert.True(d.One.IsDashing);
        }
        d.Step(None, None);
        Assert.True(d.One.CanAct);
    }

    [Fact]
    public void Its_way_is_fixed_when_pressed_not_by_the_stick_afterwards()
    {
        var d = DuelAt(400);
        d.Step(Cross, None, MoveInput.WalkBack, MoveInput.None);
        for (int t = 1; t < Dash.TotalFrames; t++) d.Step(None, None, MoveInput.WalkForward, MoveInput.None);
        Assert.Equal(-Dash.DashCm, d.One.XCm);
    }

    // ---------- Cost ----------

    [Fact]
    public void It_costs_its_stamina_and_cant_be_used_while_exhausted()
    {
        var d = DuelAt(400);
        d.Step(Cross, None);
        Assert.Equal(Rules.StaminaMax - Dash.StaminaCost, d.One.Stamina);

        var tired = DuelAt(400);
        tired.One.LoseStamina(Rules.StaminaMax);
        tired.Step(Cross, None);
        Assert.False(tired.One.IsDashing);
        Assert.Equal(0, tired.One.XCm);
    }

    // ---------- Invincible frames ----------

    [Fact]
    public void A_strike_landing_in_the_invincible_frames_passes_through()
    {
        // Touching range, so the dash in can't move: only the invincible frames can save you.
        var d = DuelAt(Rules.MinDistance);
        var jab = Book.Find("luohan_quan_1")!;
        int press = jab.StartupFrames - Dash.InvincibleFrames / 2; // the jab's active frames fall in the invincible ones
        for (int t = 0; t < 40; t++)
        {
            d.Step(t == press ? Cross : None, t == 0 ? Square : None);
            if (t == jab.StartupFrames) Assert.True(d.One.IsInvincible);
        }
        var dodge = Assert.Single(d.Hits); // the strike is spent: one just dodge, no hit later in its active frames
        Assert.True(dodge.JustDodged);
        Assert.False(dodge.Landed);
        Assert.Equal(Side.One, dodge.Defender);
        Assert.Equal(Rules.HpMax, d.One.Hp);
    }

    [Fact]
    public void A_just_dodge_gives_just_dodge_qi()
    {
        var d = DuelAt(Rules.MinDistance);
        var jab = Book.Find("luohan_quan_1")!;
        int press = jab.StartupFrames - Dash.InvincibleFrames / 2;
        for (int t = 0; t < 40; t++) d.Step(t == press ? Cross : None, t == 0 ? Square : None);
        Assert.Equal(5, Rules.JustDodgeQi);
        Assert.Equal(Rules.QiStart + Rules.JustDodgeQi, d.One.Qi);
        Assert.Equal(Rules.JustDodgeQi, d.Hits[0].DefenderQiGained);
    }

    // ---------- Combo escape ----------

    /// <summary>Two hits One at touching range with 罗汉拳 hit 1 on tick 0 (it lands on its startup frame).</summary>
    private static Duel HitByJab(out int hitTick)
    {
        var d = DuelAt(Rules.MinDistance);
        hitTick = Book.Find("luohan_quan_1")!.StartupFrames;
        for (int t = 0; t <= hitTick; t++) d.Step(None, t == 0 ? Square : None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        return d;
    }

    [Fact]
    public void The_dash_breaks_out_of_hitstun()
    {
        var d = HitByJab(out _);
        d.Step(Cross, None, MoveInput.WalkBack, MoveInput.None);
        Assert.True(d.One.IsDashing);
        Assert.True(d.One.EscapedHitstun);
        Assert.Equal(0, d.One.StunFramesLeft);
        Assert.Equal(Rules.StaminaMax - Dash.StaminaCost, d.One.Stamina);
    }

    [Fact]
    public void Nothing_else_breaks_out_of_hitstun()
    {
        var d = HitByJab(out _);
        d.Step(Square, None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
        d.Step(SlotInput.Press(Slot.L1), None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void You_cant_escape_while_exhausted()
    {
        var d = HitByJab(out _);
        d.One.LoseStamina(Rules.StaminaMax);
        d.Step(Cross, None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void A_dash_from_a_normal_state_is_not_an_escape()
    {
        var d = DuelAt(400);
        d.Step(Cross, None);
        Assert.False(d.One.EscapedHitstun);
    }

    [Fact]
    public void An_escape_dash_passes_through_strikes_but_never_earns_a_just_dodge()
    {
        // One backed against the arena edge, so the escape dash can't carry it away: only its invincible frames matter.
        int edge = -Rules.ArenaWidth / 2;
        var d = new Duel(Rules, Starter(), Starter(), edge, edge + Rules.MinDistance);
        var jab1 = Book.Find("luohan_quan_1")!;
        var jab2 = Book.Find("luohan_quan_2")!;
        // Two chains hit 2 on the first chain frame: hit 1 lands on tick 13, hit 2 on 13 + 3 + 0 + 19 = 35.
        int hit2 = jab1.StartupFrames + jab1.ActiveFrames + jab2.StartupFrames;
        int press = hit2 - Dash.InvincibleFrames / 2; // still in hit 1's hitstun (13 + 27 = 40)
        for (int t = 0; t < hit2 + 2; t++)
        {
            var two = t == 0 || d.Two.InChainWindow && d.Two.StringHit == 1 ? Square : None;
            d.Step(t == press ? Cross : None, two, MoveInput.WalkBack, MoveInput.None);
            if (t == press) Assert.True(d.One.EscapedHitstun);
        }
        var only = Assert.Single(d.Hits); // hit 1 landed; hit 2 passed through with no just dodge
        Assert.True(only.Landed);
        Assert.Equal("luohan_quan_1", only.Technique.Id);
        Assert.Equal(Rules.QiStart, d.One.Qi);
        Assert.Equal(Rules.HpMax - jab1.Damage, d.One.Hp);
    }

    [Fact]
    public void Dashing_too_early_gets_caught_once_the_invincible_frames_end()
    {
        var d = DuelAt(Rules.MinDistance);
        var jab = Book.Find("luohan_quan_1")!;
        int press = jab.StartupFrames - Dash.InvincibleFrames - 1; // invincible frames over a tick before the hit
        for (int t = 0; t < 40; t++) d.Step(t == press ? Cross : None, t == 0 ? Square : None);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.Equal(jab.StartupFrames, hit.Tick);
        Assert.Equal("qinggong_dash", hit.DefenderTechnique?.Id);
    }

    [Fact]
    public void The_dash_is_deterministic()
    {
        var a = DashFrom(300, new MoveInput(70, -70));
        var b = DashFrom(300, new MoveInput(70, -70));
        Assert.Equal(a.One.Position, b.One.Position);
    }

    // ---------- The brain uses it ----------

    private static BrainStyle Style(int step) =>
        new("test", "test", 0, 10, 0, 0, 0, 0, 0, step, 0, 0, 0, 0);

    private static void Run(Duel duel, Brain brain, int ticks, Func<int, SlotInput>? you = null, Action? each = null)
    {
        for (int i = 0; i < ticks && !duel.IsOver; i++)
        {
            var b = brain.Think(duel, Side.Two);
            duel.Step(you?.Invoke(i) ?? None, b.Slots, MoveInput.None, b.Move);
            each?.Invoke();
        }
    }

    [Fact]
    public void The_brain_dashes_in_to_close_a_long_gap()
    {
        var d = DuelAt(600);
        var brain = new Brain(Style(step: 100), 1);
        bool dashed = false;
        Run(d, brain, 30, each: () => dashed |= d.Two.IsDashing);
        Assert.True(dashed);
        Assert.Equal(BrainIntent.Closing, brain.Intent);
    }

    [Fact]
    public void The_brain_dashes_out_of_your_combo()
    {
        // You land 罗汉拳 hit 1 on the brain; it starts thinking once it's in hitstun.
        var d = DuelAt(Rules.MinDistance);
        var jab = Book.Find("luohan_quan_1")!;
        for (int t = 0; t <= jab.StartupFrames; t++) d.Step(t == 0 ? Square : None, None);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        var brain = new Brain(new BrainStyle("test", "test", 0, 10, 0, 0, 0, 0, 0, 100, 0, 0, 0, 0), 1);
        bool escaped = false;
        for (int i = 0; i < 5; i++)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(None, b.Slots, MoveInput.None, b.Move);
            escaped |= d.Two.EscapedHitstun;
        }
        Assert.True(escaped);
        Assert.Equal(BrainIntent.Escaping, brain.Intent);
    }

    [Fact]
    public void The_brain_dashes_aside_through_your_punch()
    {
        var d = DuelAt(110);
        var brain = new Brain(Style(step: 100), 1);
        bool dashed = false;
        Run(d, brain, 40, i => i == 0 ? Square : None, () => dashed |= d.Two.IsDashing);
        Assert.True(dashed);
        Assert.DoesNotContain(d.Hits, h => h.Landed);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
    }
}
