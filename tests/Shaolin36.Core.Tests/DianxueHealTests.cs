using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// 点穴 封穴 (a slow, costly finger strike with a long recovery that seals the target's 外功 for a while) and
/// 仙辽术 (spend 10 Qi, channel, heal 10 HP; a hit before the heal lands cancels it). Played on the real sheets.
/// </summary>
public class DianxueHealTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Technique T(string id) => Book.Find(id)!;
    private static Technique Finger => T("fengxue");
    private static Technique Heal => T("xianliao_shu");
    private static Technique Jab => T("luohan_quan_1");

    private static readonly SlotInput None = SlotInput.None;

    /// <summary>罗汉拳 □, 铁砂掌 △, 点穴 ○, 化绵掌 L1, 仙辽术 R1: the playtest loadout.</summary>
    private static Loadout Full()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Jab);
        l.Assign(Slot.Triangle, T("tiesha_zhang"));
        l.Assign(Slot.Circle, Finger);
        l.Assign(Slot.L1, T("timed_parry"));
        l.Assign(Slot.R1, Heal);
        return l;
    }

    private static Duel DuelAt(int apart, int? oneQi = null, int? twoQi = null) => new(Rules, Full(), Full(), 0, apart, oneQi, twoQi);

    // ---------- The sheet ----------

    [Fact]
    public void Fengxue_is_slower_dearer_and_slower_to_recover_than_a_luohan_quan_hit()
    {
        Assert.Equal(Category.Dianxue, Finger.Category);
        Assert.True(Finger.StartupFrames > Jab.StartupFrames, "longer wind-up");
        Assert.True(Finger.StaminaCost > Jab.StaminaCost, "more stamina");
        Assert.Equal(Sim.TicksPerSecond, Finger.RecoveryFrames); // Chan: about 1 s to recover, no skill cooldown
        Assert.True(Finger.SealFrames > 0);
        Assert.True(Finger.Damage > 0 && Finger.Damage < Jab.Damage + 1, "small damage");
        Assert.False(Finger.Clashable);
    }

    [Fact]
    public void Xianliao_shu_spends_10_qi_to_heal_10_hp_and_never_hits()
    {
        Assert.Equal(Category.Qigong, Heal.Category);
        Assert.Equal(10, Heal.QiCost);
        Assert.Equal(10, Heal.Heal);
        Assert.Equal(0, Heal.ReachCm);
        Assert.True(Heal.StartupFrames >= 20 && Heal.ActiveFrames >= 30 && Heal.RecoveryFrames > 0, "a wind-up, a channel and a finish");
    }

    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry,seal,heal\n";

    [Theory]
    [InlineData("x,x,waigong,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0,60,0", "only 点穴")]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,0,,0,0,60,0", "never hits")]
    [InlineData("x,x,waigong,light,false,10,3,10,0,0,5,0,0,false,0,0,,0,0,0,10", "only 气功")]
    [InlineData("x,x,qigong,light,false,10,3,10,0,0,5,0,0,false,0,100,,0,0,0,10", "can't also be a strike")]
    [InlineData("x,x,qigong,light,false,10,3,0,0,0,5,0,0,false,0,0,,0,0,0,10", "needs 'recovery'")]
    [InlineData("x,x,waigong,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0,99999,0", "'seal'")]
    public void Bad_seal_and_heal_cells_are_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Seal_and_heal_columns_may_be_left_out()
    {
        var r = TechniqueCsv.Parse(Header.Replace(",seal,heal", "") + "x,x,waigong,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var t = r.Techniques.Single();
        Assert.Equal(0, t.SealFrames);
        Assert.Equal(0, t.Heal);
    }

    // ---------- Recovery, no cooldown ----------

    [Fact]
    public void Fengxue_can_be_used_again_as_soon_as_its_recovery_ends()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Circle, Finger);
        var f = new Fighter(l, 120);
        f.Tick(SlotInput.Press(Slot.Circle));
        for (int i = 1; i < Finger.TotalFrames; i++) f.Tick(None);
        f.Tick(SlotInput.Press(Slot.Circle));
        Assert.Same(Finger, f.Technique);
        Assert.Equal(0, f.MoveFrame);
    }

    [Fact]
    public void A_hit_during_fengxue_recovery_interrupts_it()
    {
        // One's 封穴 whiffs from 300 cm away; Two walks in and jabs One during the 1 s recovery.
        var d = DuelAt(300);
        int recoveryStart = Finger.StartupFrames + Finger.ActiveFrames;
        for (int t = 0; t < recoveryStart + 5; t++) d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
        Assert.Equal(FighterState.Recovery, d.One.State);
        // Two walks in and throws 罗汉拳 hit 1 while One is still recovering.
        int walked = 0;
        while (d.DistanceCm > 110 && walked++ < 60) d.Step(None, None, MoveInput.None, MoveInput.WalkForward);
        Assert.Equal(FighterState.Recovery, d.One.State);
        for (int t = 0; t <= Jab.StartupFrames; t++) d.Step(None, t == 0 ? SlotInput.Press(Slot.Square) : None);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    // ---------- Seal ----------

    [Fact]
    public void A_sealed_fighter_cannot_start_waigong_but_can_still_parry()
    {
        var f = new Fighter(Full(), 120);
        f.Seal(100);
        for (int call = 1; call < 100; call++)
        {
            f.Tick(SlotInput.Press(Slot.Square));
            Assert.False(f.State.IsInMove(), $"罗汉拳 started on call {call} while sealed");
        }
        f.Tick(SlotInput.Press(Slot.Square));
        Assert.Same(Jab, f.Technique);

        var g = new Fighter(Full(), 120);
        g.Seal(100);
        g.Tick(SlotInput.Press(Slot.Triangle));
        Assert.False(g.State.IsInMove());
        g.Tick(SlotInput.Press(Slot.L1));
        Assert.Equal(Category.Huajin, g.Technique!.Category);
    }

    [Fact]
    public void A_seal_stops_a_string_carrying_on()
    {
        var f = new Fighter(Full(), 120);
        f.Tick(SlotInput.Press(Slot.Square));
        while (!f.InChainWindow) f.Tick(None);
        f.Seal(60);
        f.Tick(SlotInput.Press(Slot.Square));
        Assert.Same(Jab, f.Technique);
        Assert.Equal(1, f.StringHit);
    }

    [Fact]
    public void Seals_refresh_and_do_not_stack()
    {
        var f = new Fighter(Full(), 120);
        f.Seal(100);
        f.Seal(40);
        Assert.Equal(100, f.SealedFrames);
        f.Seal(150);
        Assert.Equal(150, f.SealedFrames);
    }

    [Fact]
    public void A_landed_dianxue_deals_small_damage_and_seals_waigong_for_its_seal_frames()
    {
        var d = DuelAt(120);
        int hitTick = Finger.StartupFrames;
        HitEvent? hit = null;
        int jabStarted = -1;
        bool parried = false;
        for (int t = 0; t <= hitTick + Finger.SealFrames + 2; t++)
        {
            // Two mashes □ after the hitstun, and tries its parry once mid-seal.
            var two = t == hitTick + 80 ? SlotInput.Press(Slot.L1) : t > hitTick + Finger.HitstunFrames ? SlotInput.Press(Slot.Square) : None;
            var hits = d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, two);
            if (hits.Count > 0 && hit == null) hit = hits[0];
            if (jabStarted < 0 && d.Two.Technique == Jab) jabStarted = t;
            parried |= d.Two.Technique?.Category == Category.Huajin;
        }
        Assert.NotNull(hit);
        Assert.True(hit!.Landed);
        Assert.Equal(hitTick, hit.Tick);
        Assert.Equal(Finger.Damage, hit.Damage);
        Assert.Equal(Finger.SealFrames, hit.SealFrames);
        Assert.True(parried, "other categories still work while sealed");
        Assert.True(jabStarted >= hitTick + Finger.SealFrames, $"罗汉拳 came out on tick {jabStarted}, inside the seal");
    }

    [Fact]
    public void A_parried_dianxue_seals_nothing()
    {
        var d = DuelAt(120);
        var parry = T("timed_parry");
        int pressAt = Finger.StartupFrames - parry.StartupFrames - 1; // meets the hit on the parry's 2nd perfect frame
        for (int t = 0; t <= Finger.StartupFrames; t++)
            d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, t == pressAt ? SlotInput.Press(Slot.L1) : None);
        var hit = d.Hits.Single();
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(0, hit.SealFrames);
        Assert.False(d.Two.IsSealed);
    }

    // ---------- 仙辽术 ----------

    [Fact]
    public void Xianliao_shu_heals_10_hp_when_its_channel_ends_and_spends_10_qi_at_the_start()
    {
        var f = new Fighter(Full(), 120, maxQi: 100, qi: 25);
        f.TakeHit(30, 0);
        f.Tick(SlotInput.Press(Slot.R1));
        Assert.Same(Heal, f.Technique);
        Assert.Equal(15, f.Qi);
        int healedOn = -1;
        for (int frame = 1; frame < Heal.TotalFrames; frame++)
        {
            f.Tick(None);
            if (f.HealedThisTick > 0) { Assert.Equal(-1, healedOn); healedOn = frame; Assert.Equal(10, f.HealedThisTick); }
        }
        Assert.Equal(Heal.StartupFrames + Heal.ActiveFrames, healedOn);
        Assert.Equal(100, f.Hp);
    }

    [Fact]
    public void Xianliao_shu_never_heals_above_max_hp()
    {
        var f = new Fighter(Full(), 120, maxQi: 100, qi: 10);
        f.TakeHit(4, 0);
        f.Tick(SlotInput.Press(Slot.R1));
        for (int frame = 1; frame < Heal.TotalFrames; frame++) f.Tick(None);
        Assert.Equal(120, f.Hp);
    }

    [Fact]
    public void Xianliao_shu_needs_10_qi()
    {
        var f = new Fighter(Full(), 120, maxQi: 100, qi: 9);
        f.Tick(SlotInput.Press(Slot.R1));
        Assert.False(f.State.IsInMove());
        Assert.Equal(9, f.Qi);
    }

    [Fact]
    public void A_hit_during_the_channel_cancels_the_heal_and_the_qi_is_lost()
    {
        var d = DuelAt(120, oneQi: 20);
        d.One.TakeHit(30, 0);
        int hpBefore = d.One.Hp;
        // One starts healing on tick 0; Two throws 罗汉拳 hit 1 on tick 40, landing mid-channel.
        bool healed = false;
        for (int t = 0; t < Heal.TotalFrames + 20; t++)
        {
            d.Step(t == 0 ? SlotInput.Press(Slot.R1) : None, t == 40 ? SlotInput.Press(Slot.Square) : None);
            healed |= d.One.HealedThisTick > 0;
        }
        Assert.False(healed);
        Assert.Equal(10, d.One.Qi);
        Assert.Equal(hpBefore - Jab.Damage, d.One.Hp);
    }

    // ---------- The brain ----------

    private static BrainStyle Style(int aggression = 0, int punish = 0, int heal = 0) =>
        new("test", "test", 0, 10, aggression, punish, 0, 0, 0, 0, 0, 0, 0, 0, heal);

    private static void Run(Duel d, Brain brain, int ticks, Func<int, SlotInput>? you = null, Action<int>? each = null)
    {
        for (int i = 0; i < ticks && !d.IsOver; i++)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(you?.Invoke(i) ?? None, b.Slots, MoveInput.None, b.Move);
            each?.Invoke(i);
        }
    }

    [Fact]
    public void The_brain_heals_when_hurt_with_qi_and_well_out_of_reach()
    {
        var d = DuelAt(400, twoQi: 20);
        d.Two.TakeHit(30, 0);
        var brain = new Brain(Style(heal: 100), 1);
        Run(d, brain, Heal.TotalFrames + 5);
        Assert.Equal(100, d.Two.Hp);
        Assert.Equal(10, d.Two.Qi);
    }

    [Fact]
    public void The_brain_does_not_heal_in_reach()
    {
        var d = DuelAt(150, twoQi: 20);
        d.Two.TakeHit(30, 0);
        var brain = new Brain(Style(heal: 100), 1);
        Run(d, brain, 120, each: _ => Assert.NotSame(Heal, d.Two.Technique));
    }

    [Fact]
    public void The_brain_punishes_your_heal()
    {
        var d = DuelAt(120, oneQi: 20);
        d.One.TakeHit(30, 0);
        var brain = new Brain(Style(punish: 100), 1);
        bool healed = false;
        Run(d, brain, Heal.TotalFrames, you: i => i == 0 ? SlotInput.Press(Slot.R1) : None, each: _ => healed |= d.One.HealedThisTick > 0);
        Assert.False(healed);
        Assert.Contains(d.Hits, h => h.Attacker == Side.Two && h.Landed);
    }

    [Fact]
    public void A_sealed_brain_does_not_throw_waigong()
    {
        var d = DuelAt(120);
        d.Two.Seal(600);
        var brain = new Brain(Style(aggression: 100), 1);
        Run(d, brain, 300, each: _ => Assert.NotEqual(Category.Waigong, d.Two.Technique?.Category));
    }

    [Fact]
    public void The_brain_uses_dianxue()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Circle, Finger);
        var d = new Duel(Rules, Full(), l, 0, 120);
        var brain = new Brain(Style(aggression: 100), 1);
        bool used = false;
        Run(d, brain, 60, each: _ => used |= d.Two.Technique == Finger);
        Assert.True(used);
    }

    [Fact]
    public void The_brain_does_not_throw_fengxue_at_a_fighter_already_sealed()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Circle, Finger);
        var d = new Duel(Rules, Full(), l, 0, 120);
        d.One.Seal(600);
        var brain = new Brain(Style(aggression: 100), 1);
        Run(d, brain, 300, each: _ => Assert.NotSame(Finger, d.Two.Technique));
    }

    [Fact]
    public void The_brain_sheet_heal_column_is_read_and_may_be_left_out()
    {
        var sheet = BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv")));
        Assert.True(sheet.Ok, string.Join("\n", sheet.Errors));
        Assert.All(sheet.Sheet!.Styles, s => Assert.InRange(s.Heal, 1, 100));

        var old = BrainCsv.Parse("id,name,reaction,think_every,aggression,punish,chain,parry,perfect,step,circle,rest_below,heavy,guess\n" +
                                 "a,a,10,10,10,10,10,10,10,10,10,10,10,10\n");
        Assert.True(old.Ok, string.Join("\n", old.Errors));
        Assert.Equal(0, old.Sheet!.Styles[0].Heal);
    }
}
