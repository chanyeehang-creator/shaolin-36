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
        Assert.True(Finger.Clashable); // Chan: light and clashable, so its wind-up can be read and clashed
        Assert.Equal(Weight.Light, Finger.Weight);
        Assert.Equal(0, Finger.AdvanceCm); // stands still through the wind-up
        Assert.InRange(Finger.LungeCm, 91, 199); // Chan: between the quick step (70 / 90 cm) and the 轻功 dash (200 cm)
    }

    [Fact]
    public void Xianliao_shu_spends_10_qi_to_heal_10_hp_and_never_hits()
    {
        Assert.Equal(Category.Lianqi, Heal.Category);
        Assert.Equal(10, Heal.QiCost);
        Assert.Equal(10, Heal.Heal);
        Assert.Equal(0, Heal.ReachCm);
        Assert.True(Heal.StartupFrames >= 20 && Heal.ActiveFrames >= 30 && Heal.RecoveryFrames > 0, "a wind-up, a channel and a finish");
    }

    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry,seal,heal\n";

    [Theory]
    [InlineData("x,x,waigong,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0,60,0", "only 点穴")]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,0,,0,0,60,0", "never hits")]
    [InlineData("x,x,waigong,light,false,10,3,10,0,0,5,0,0,false,0,0,,0,0,0,10", "only 炼气")]
    [InlineData("x,x,lianqi,light,false,10,3,10,0,0,5,0,0,false,0,100,,0,0,0,10", "can't also be a strike")]
    [InlineData("x,x,lianqi,light,false,10,3,0,0,0,5,0,0,false,0,0,,0,0,0,10", "needs 'recovery'")]
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

    // ---------- Lunge ----------

    [Fact]
    public void Fengxue_stands_still_in_its_wind_up_then_lunges_on_its_first_active_frame()
    {
        var d = DuelAt(400);
        for (int t = 0; t < Finger.StartupFrames; t++)
        {
            d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
            Assert.Equal(0, d.One.XCm);
        }
        for (int t = 0; t < Finger.ActiveFrames; t++) d.Step(None, None);
        Assert.Equal(Finger.LungeCm, d.One.XCm); // the whole lunge is done by the last active frame
        d.Step(None, None);
        Assert.Equal(FighterState.Recovery, d.One.State);
        Assert.Equal(Finger.LungeCm, d.One.XCm);
    }

    [Fact]
    public void The_lunge_carries_the_hit_so_fengxue_connects_from_well_past_its_reach()
    {
        int far = Finger.FullReachCm - 5; // 250 cm: more than double the 115 cm hitbox
        var d = DuelAt(far);
        for (int t = 0; t < Finger.StartupFrames + Finger.ActiveFrames; t++) d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.True(d.Two.IsSealed);
    }

    [Fact]
    public void Out_of_lunge_range_fengxue_whiffs()
    {
        var d = DuelAt(Finger.FullReachCm + 10);
        for (int t = 0; t < Finger.TotalFrames; t++) d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
        Assert.Empty(d.Hits);
    }

    [Fact]
    public void The_lunge_stops_at_min_distance()
    {
        var d = DuelAt(120);
        for (int t = 0; t < Finger.StartupFrames + Finger.ActiveFrames; t++) d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
        Assert.Equal(Rules.MinDistance, d.DistanceCm);
    }

    [Theory]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,0,,0,0,0,0,140", "'reach' is 0")]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0,0,0,999", "'lunge' (999)")]
    public void Bad_lunge_cells_are_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header.Replace(",heal\n", ",heal,lunge\n") + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains(expected));
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

    // ---------- Recovery: short on a hit, long on a miss (Chan's balance patch) ----------

    [Fact]
    public void Fengxue_recovers_in_24_frames_after_a_hit_and_stays_ahead_of_its_hitstun()
    {
        Assert.Equal(24, Finger.HitRecoveryFrames);
        Assert.True(Finger.HitRecoveryFrames < Finger.HitstunFrames, "free before the defender, but only just");
        Assert.True(Finger.HitstunFrames - Finger.HitRecoveryFrames <= 6, "a brief edge, not a free combo");
    }

    [Fact]
    public void A_landed_fengxue_frees_the_attacker_hit_recovery_frames_after_the_hit()
    {
        var d = DuelAt(120);
        int hitTick = -1;
        for (int t = 0; t < Finger.TotalFrames && hitTick < 0; t++)
            if (d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None).Any(h => h.Landed)) hitTick = t;
        Assert.True(hitTick > 0, "封穴 should land from 120 cm");
        int free = -1;
        for (int t = hitTick + 1; t < hitTick + Finger.TotalFrames && free < 0; t++)
        {
            d.Step(None, None);
            if (d.One.CanAct) free = t;
        }
        Assert.Equal(hitTick + Finger.HitRecoveryFrames, free);
        Assert.Equal(FighterState.Hitstun, d.Two.State); // still reeling: the attacker is a few frames ahead
    }

    [Fact]
    public void A_whiffed_fengxue_keeps_its_full_1_s_recovery()
    {
        var d = DuelAt(300);
        for (int t = 0; t < Finger.TotalFrames; t++)
        {
            d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, None);
            Assert.False(d.One.CanAct, $"free on tick {t}, before the whiff recovery ended");
        }
        d.Step(None, None);
        Assert.True(d.One.CanAct);
        Assert.Empty(d.Hits);
    }

    [Fact]
    public void A_parried_fengxue_keeps_its_full_recovery()
    {
        // Blocked (not perfect-parried, which staggers): no hit landed, so no short recovery.
        var d = DuelAt(120);
        var parry = T("timed_parry");
        int pressAt = Finger.StartupFrames - parry.StartupFrames - 1; // meets the hit in the early block frames
        int t = 0;
        for (; t <= Finger.StartupFrames; t++)
            d.Step(t == 0 ? SlotInput.Press(Slot.Circle) : None, t == pressAt ? SlotInput.Press(Slot.L1) : None);
        Assert.Equal(ParryOutcome.Parried, d.Hits.Single().Parry);
        for (; t < Finger.TotalFrames - 1; t++)
        {
            d.Step(None, None);
            Assert.False(d.One.CanAct, $"free on tick {t} after a blocked 封穴");
        }
    }

    [Theory]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,0,,0,0,0,0,,5", "'hit_recovery' is set but 'reach' is 0")]
    [InlineData("x,x,dianxue,light,false,10,3,10,0,0,5,0,4,false,10,100,,0,0,0,0,,13", "must be shorter than 'active' + 'recovery'")]
    [InlineData("x,x,huajin,light,false,2,10,10,0,0,5,0,0,false,0,0,,0,4,0,0,7,", "'early_block' + 'perfect_parry' (7 + 4) cannot be longer than 'active' (10)")]
    [InlineData("x,x,waigong,light,false,2,10,10,0,0,5,0,0,false,0,100,,0,0,0,0,3,", "'early_block' is set but 'perfect_parry' is 0")]
    public void Bad_early_block_and_hit_recovery_cells_are_reported(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header.Replace(",heal\n", ",heal,early_block,hit_recovery\n") + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    // ---------- Seal ----------

    [Fact]
    public void A_sealed_fighter_cannot_start_luohan_quan_but_can_still_use_tiesha_zhang_and_parry()
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

        // 铁砂掌 is 内功 (Chan: a heavy strike), so the seal doesn't stop it.
        var g = new Fighter(Full(), 120);
        g.Seal(100);
        g.Tick(SlotInput.Press(Slot.Triangle));
        Assert.Equal("tiesha_zhang", g.Technique!.Id);
        Assert.Equal(Category.Neigong, g.Technique.Category);

        var h = new Fighter(Full(), 120);
        h.Seal(100);
        h.Tick(SlotInput.Press(Slot.L1));
        Assert.Equal(Category.Fangshen, h.Technique!.Category);
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
            parried |= d.Two.Technique?.Category == Category.Fangshen;
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
        int pressAt = Finger.StartupFrames - parry.PerfectParryStart - 1; // meets the hit on the parry's 2nd perfect frame
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
