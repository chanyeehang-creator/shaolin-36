using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Knocked down (the 擒拿手 throw): lying on the floor, nothing can hit you (no move has hits_down yet). After down_min
/// frames any button rises and a direction rolls away, both invincible; after down_max you rise by yourself; exhausted, you
/// stay down until stamina is back to exhausted_until. Played on the real sheets.
/// </summary>
public class KnockdownTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly List<Technique> Sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
    private static readonly TechniqueBook Book = new(Sheet);

    private static Technique T(string id) => Book.Find(id)!;
    private static readonly SlotInput None = SlotInput.None;
    private static SlotInput P(Slot s) => SlotInput.Press(s);

    private static Loadout Full(Technique? extra = null)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("luohan_quan_1"));
        l.Assign(Slot.Triangle, T("tiesha_zhang"));
        l.Assign(Slot.Circle, T("fengxue"));
        l.Assign(Slot.Cross, T("qinggong_dash"));
        l.Assign(Slot.L1, T("timed_parry"));
        l.Assign(Slot.R1, T("xianliao_shu"));
        l.Assign(Slot.R2, T("qinna_shou"));
        if (extra != null) l.Assign(Slot.L2, extra);
        return l;
    }

    /// <summary>Two lies knocked down 100 cm from One, knocked down just before tick 0 (so tick N is its frame N + 1 on the floor).</summary>
    private static Duel Downed(Technique? oneExtra = null, int? twoQi = null)
    {
        var d = new Duel(Rules, Full(oneExtra), Full(), 0, 100, null, twoQi);
        d.Two.KnockDown();
        return d;
    }

    /// <summary>Step until Two is no longer knocked down; returns the tick it got up on.</summary>
    private static int GetUpTick(Duel d, Func<int, SlotInput>? two = null, MoveInput twoMove = default)
    {
        while (d.Two.State == FighterState.KnockedDown) d.Step(None, two?.Invoke(d.Tick) ?? None, MoveInput.None, twoMove);
        return d.Tick - 1;
    }

    private static Technique Strike(string id, bool knockdown = false, bool hitsDown = false) =>
        new(id, id, Category.Waigong, Weight.Medium, false, 5, 3, 10, 0, 0, 5, 0, 7, false, 20, 120,
            knockdown: knockdown, hitsDown: hitsDown, knockbackCm: 0);

    // ---------- Nothing hits you on the floor ----------

    [Fact]
    public void No_move_on_the_sheet_hits_a_downed_fighter_yet()
    {
        Assert.All(Sheet, t => Assert.False(t.HitsDown, $"{t.Id} has hits_down"));
    }

    [Fact]
    public void Strikes_pass_over_a_downed_fighter()
    {
        foreach (var slot in new[] { Slot.Square, Slot.Triangle, Slot.Circle })
        {
            var d = Downed();
            for (int i = 0; i < 18; i++)
            {
                Assert.Empty(d.Step(i == 0 ? P(slot) : None, None));
                Assert.Equal(FighterState.KnockedDown, d.Two.State);
            }
            Assert.Equal(Rules.HpMax, d.Two.Hp);
        }
    }

    [Fact]
    public void A_grab_cant_catch_a_downed_fighter()
    {
        var d = Downed();
        for (int i = 0; i < 16; i++) Assert.Empty(d.Step(i == 0 ? P(Slot.R2) : None, None));
        Assert.False(d.One.IsHolding);
    }

    [Fact]
    public void A_hits_down_move_deals_damage_and_leaves_them_down()
    {
        var d = Downed(Strike("stomp", hitsDown: true));
        var hits = new List<HitEvent>();
        for (int i = 0; i < 10; i++) hits.AddRange(d.Step(i == 0 ? P(Slot.L2) : None, None));
        var hit = Assert.Single(hits);
        Assert.True(hit.Landed);
        Assert.Equal(7, hit.Damage);
        Assert.Equal(0, hit.HitstunFrames);
        Assert.Equal(FighterState.KnockedDown, d.Two.State);
    }

    // ---------- Getting up ----------

    [Fact]
    public void Any_button_rises_once_down_min_has_passed()
    {
        var d = Downed();
        int up = GetUpTick(d, _ => P(Slot.L1)); // mashing from the start
        Assert.Equal(Rules.DownMin - 1, up);
        Assert.Equal(FighterState.Rising, d.Two.State);
        Assert.Null(d.Two.Technique); // the button only rose: no parry came out
        while (d.Two.State == FighterState.Rising) d.Step(None, None);
        Assert.Equal(up + Rules.RiseFrames, d.Tick - 1);
        Assert.True(d.Two.CanAct);
    }

    [Fact]
    public void Left_alone_you_rise_by_yourself_after_down_max()
    {
        var d = Downed();
        Assert.Equal(Rules.DownMax - 1, GetUpTick(d));
        Assert.Equal(FighterState.Rising, d.Two.State);
    }

    [Fact]
    public void A_direction_rolls_away()
    {
        var d = Downed();
        int before = d.DistanceCm;
        GetUpTick(d, twoMove: MoveInput.WalkBack);
        Assert.Equal(FighterState.Rolling, d.Two.State);
        while (d.Two.State == FighterState.Rolling) d.Step(None, None);
        Assert.True(d.Two.CanAct);
        Assert.InRange(d.DistanceCm, before + Rules.RollDistance - 2, before + Rules.RollDistance + 2);
    }

    [Fact]
    public void Rising_and_rolling_are_invincible_but_no_just_dodge()
    {
        foreach (var roll in new[] { false, true })
        {
            var d = Downed();
            // Rise (or roll sideways, staying in reach) on tick down_min; a 罗汉拳 hit 1 pressed 8 ticks before lands mid-rise.
            int up = Rules.DownMin - 1;
            var hits = new List<HitEvent>();
            while (d.Tick <= up + 10)
            {
                int t = d.Tick;
                hits.AddRange(d.Step(t == up - 8 ? P(Slot.Square) : None, t == up && !roll ? P(Slot.Square) : None,
                    MoveInput.None, t == up && roll ? MoveInput.WalkRight : MoveInput.None));
            }
            Assert.Empty(hits);
            Assert.Equal(Rules.HpMax, d.Two.Hp);
        }
    }

    [Fact]
    public void Exhausted_you_stay_down_until_stamina_is_back()
    {
        var d = Downed();
        d.Two.LoseStamina(d.Two.Stamina);
        Assert.True(d.Two.IsExhausted);
        int up = GetUpTick(d, _ => P(Slot.Square));
        Assert.True(up > Rules.DownMax, "stays down past down_max while exhausted");
        Assert.False(d.Two.IsExhausted);
        Assert.True(d.Two.Stamina >= Rules.ExhaustedUntil);
    }

    // ---------- Knockdown on any move ----------

    [Fact]
    public void A_knockdown_strike_puts_them_on_the_floor_but_not_through_armor()
    {
        var d = new Duel(Rules, Full(Strike("sweep", knockdown: true)), Full(), 0, 100);
        var hits = new List<HitEvent>();
        for (int i = 0; i < 6; i++) hits.AddRange(d.Step(i == 0 ? P(Slot.L2) : None, None));
        Assert.True(Assert.Single(hits).KnockedDown);
        Assert.Equal(FighterState.KnockedDown, d.Two.State);

        var armored = new Duel(Rules, Full(Strike("sweep", knockdown: true)), Full(), 0, 100, null, 30);
        hits.Clear();
        for (int i = 0; i < 6; i++) hits.AddRange(armored.Step(i == 0 ? P(Slot.L2) : None, i == 0 ? P(Slot.Triangle) : None));
        Assert.False(Assert.Single(hits).KnockedDown);
        Assert.NotEqual(FighterState.KnockedDown, armored.Two.State);
    }

    // ---------- The opponent brain ----------

    private static BrainStyle Style(int step) =>
        new("t", "t", reaction: 0, thinkEvery: 1, aggression: 100, punish: 100, chain: 0, parry: 0, perfect: 0, step: step,
            circle: 0, restBelow: 0, heavy: 0, guess: 0, heal: 0, grab: 100);

    [Theory]
    [InlineData(0, FighterState.Rising)]
    [InlineData(100, FighterState.Rolling)]
    public void The_brain_gets_up_as_soon_as_it_can(int step, FighterState expected)
    {
        var d = Downed();
        var brain = new Brain(Style(step), 1);
        while (d.Two.State == FighterState.KnockedDown)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(None, b.Slots, MoveInput.None, b.Move);
        }
        Assert.Equal(expected, d.Two.State);
        Assert.InRange(d.Tick - 1, Rules.DownMin - 1, Rules.DownMin);
    }

    [Fact]
    public void The_brain_doesnt_waste_attacks_on_a_downed_fighter()
    {
        var d = new Duel(Rules, Full(), Full(), 0, 100);
        d.One.KnockDown();
        var brain = new Brain(Style(0), 1);
        for (int i = 0; i < Rules.DownMin; i++)
        {
            d.Step(None, brain.Think(d, Side.Two).Slots);
            Assert.Null(d.Two.Technique);
        }
    }

    // ---------- Sheet checks ----------

    [Fact]
    public void Down_max_below_down_min_is_reported()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"), ("down_min", "50"), ("down_max", "10")));
        Assert.Contains(r.Errors, e => e.Contains("down_max"));
    }

    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry,hold,knockdown,hits_down\n";

    [Theory]
    [InlineData("x,x,waigong,light,false,10,4,30,0,0,15,0,14,false,10,0,,0,0,0,true,false", "'knockdown' is set")]
    [InlineData("x,x,waigong,light,false,10,4,30,0,0,15,0,14,false,10,0,,0,0,0,false,true", "'hits_down' is set")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,0,100,,0,0,30,true,true", "downed fighter")]
    [InlineData("x,x,qinna,light,false,10,4,30,0,0,15,0,14,false,0,100,,0,0,30,false,false", "'knockdown' true")]
    public void Bad_knockdown_cells_are_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }
}
