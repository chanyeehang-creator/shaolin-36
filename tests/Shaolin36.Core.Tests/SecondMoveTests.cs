using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// The second move in each category (Chan's brief): 虎形拳, 狮吼功, 纵云梯, 无相劫指, 硬气功, 龙爪手 and 易筋经, and the grab
/// break-out. Played on the real sheets.
/// </summary>
public class SecondMoveTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Technique T(string id) => Book.Find(id)!;

    private static readonly SlotInput None = SlotInput.None;
    private static SlotInput P(Slot s) => SlotInput.Press(s);

    /// <summary>The playtest's first set: 罗汉拳 □, 铁砂掌 △, 封穴 ○, dash ✕, 化绵掌 L1, 仙辽术 R1, 小擒拿手 R2.</summary>
    private static Loadout First()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("luohan_quan_1"));
        l.Assign(Slot.Triangle, T("tiesha_zhang"));
        l.Assign(Slot.Circle, T("fengxue"));
        l.Assign(Slot.Cross, T("qinggong_dash"));
        l.Assign(Slot.L1, T("timed_parry"));
        l.Assign(Slot.R1, T("xianliao_shu"));
        l.Assign(Slot.R2, T("qinna_shou"));
        return l;
    }

    /// <summary>The second set on the same buttons: 虎形拳 □, 狮吼功 △, 无相劫指 ○, 纵云梯 ✕, 硬气功 L1, 易筋经 R1, 龙爪手 R2.</summary>
    private static Loadout Second()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, T("huxing_quan_1"));
        l.Assign(Slot.Triangle, T("shihou_gong"));
        l.Assign(Slot.Circle, T("wuxiang_zhi_1"));
        l.Assign(Slot.Cross, T("zongyun_ti"));
        l.Assign(Slot.L1, T("yingqi_gong"));
        l.Assign(Slot.R1, T("yijinjing"));
        l.Assign(Slot.R2, T("longzhua_shou"));
        return l;
    }

    private static Duel DuelOf(Loadout one, Loadout two, int apart, int? oneQi = null, int? twoQi = null) =>
        new(Rules, one, two, 0, apart, oneQi, twoQi);

    /// <summary>Step until tick <paramref name="tick"/> has run, with inputs chosen per tick; returns every event.</summary>
    private static List<HitEvent> RunTo(Duel d, int tick, Func<int, SlotInput>? one = null, Func<int, SlotInput>? two = null)
    {
        var all = new List<HitEvent>();
        while (d.Tick <= tick && !d.IsOver) { int t = d.Tick; all.AddRange(d.Step(one?.Invoke(t) ?? None, two?.Invoke(t) ?? None)); }
        return all;
    }

    private static Func<int, SlotInput> At(int tick, Slot slot) => t => t == tick ? P(slot) : None;
    private static Func<int, SlotInput> AtTicks(Slot slot, params int[] ticks) => t => ticks.Contains(t) ? P(slot) : None;
    private static Func<int, SlotInput> Holding(Slot slot, int from = 0, int until = int.MaxValue) =>
        t => t == from ? P(slot) : t > from && t < until ? SlotInput.Hold(slot) : None;

    // ---------- The sheet ----------

    [Fact]
    public void The_seven_second_moves_are_on_the_sheet_as_Chan_described()
    {
        var luohan = new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" }.Select(T).ToList();
        var huxing = new[] { "huxing_quan_1", "huxing_quan_2", "huxing_quan_3" }.Select(T).ToList();
        Assert.All(huxing, h => Assert.Equal(Category.Waigong, h.Category));
        Assert.All(huxing, h => Assert.Equal(Weight.Medium, h.Weight));
        Assert.True(huxing.Sum(h => h.Damage) < luohan.Sum(l => l.Damage), "虎形拳 deals less in all than 罗汉拳");
        Assert.True(huxing.Min(h => h.AdvanceCm) > luohan.Max(l => l.AdvanceCm), "each 虎形拳 hit leaps farther than any 罗汉拳 step");
        Assert.True(huxing[0].StartupFrames > luohan[0].StartupFrames, "hit 1 winds up a little longer");
        Assert.True(huxing[1].StartupFrames < huxing[0].StartupFrames && huxing[2].StartupFrames < huxing[0].StartupFrames, "then two quick hits");

        var roar = T("shihou_gong");
        Assert.Equal(Category.Neigong, roar.Category);
        Assert.True(roar.IsEnergy);
        Assert.Equal(5, roar.QiCost);
        Assert.Equal(20, roar.Damage);
        Assert.True(roar.Knockdown);
        Assert.Equal(0, roar.AdvanceCm + roar.LungeCm);
        Assert.Equal(Technique.FullCircle, roar.HitAngle);
        Assert.True(roar.ReachCm >= 200, "a large ring");

        var jump = T("zongyun_ti");
        Assert.True(jump.IsJump);
        Assert.Equal(T("qinggong_dash").StaminaCost, jump.StaminaCost);
        Assert.True(jump.DashCm > T("qinggong_dash").DashCm, "big horizontal reach");
        Assert.True(jump.InvincibleFrames > 0 && jump.InvincibleFrames < T("qinggong_dash").InvincibleFrames, "a small i-frame on take-off");
        Assert.False(jump.CanEscape);

        var finger = T("wuxiang_zhi_1");
        var finger2 = T("wuxiang_zhi_2");
        Assert.Equal(Category.Dianxue, finger.Category);
        Assert.False(finger.AutoChain, "Chan: tap twice for both hits");
        Assert.True(finger.ChainWindowFrames > 0);
        Assert.True(finger.StartupFrames <= 3, "almost no wind-up");
        Assert.True(finger.Damage + finger2.Damage > T("fengxue").Damage);
        Assert.True(finger.Weight == Weight.Light && finger2.Weight == Weight.Light);
        Assert.True(finger.FullReachCm < T("luohan_quan_1").FullReachCm, "very close range");
        Assert.True(finger2.CrippleFrames > 0);
        Assert.Equal(T("luohan_quan_1").RecoveryFrames, finger2.RecoveryFrames);

        var guard = T("yingqi_gong");
        Assert.Equal(Category.Fangshen, guard.Category);
        Assert.Equal(Category.Fangshen, T("timed_parry").Category);
        Assert.True(guard.IsGuard);
        Assert.True(guard.IsGuarding(0), "guards from the very first frame");
        Assert.Equal(0, guard.StaminaCost);
        Assert.Equal(0, guard.PerfectParryFrames);

        var claw = T("longzhua_shou");
        Assert.True(claw.IsMultiGrab);
        Assert.Equal(3, claw.GrabHits);
        Assert.True(claw.Damage * claw.GrabHits > T("qinna_shou").Damage);
        Assert.Equal("小擒拿手 (grab)", T("qinna_shou").Name);

        var qi = T("yijinjing");
        var heal = T("xianliao_shu");
        Assert.Equal(Category.Lianqi, qi.Category);
        Assert.Equal(10, qi.QiCost);
        Assert.Equal(600, qi.BuffFrames); // Chan: 10 s
        Assert.Equal(150, qi.BuffDamagePct);
        Assert.Equal(heal.TotalFrames, qi.TotalFrames);
    }

    // ---------- 虎形拳 ----------

    [Fact]
    public void Huxing_quan_lands_all_three_leaping_hits_from_farther_out_than_luohan_quan_reaches()
    {
        var d = DuelOf(Second(), Second(), 150);
        var hits = RunTo(d, 120, one: t => t % 2 == 0 && t < 50 ? P(Slot.Square) : None);
        var landed = hits.Where(h => h.Attacker == Side.One && h.Landed).ToList();
        Assert.Equal(new[] { "huxing_quan_1", "huxing_quan_2", "huxing_quan_3" }, landed.Select(h => h.Technique.Id));
        Assert.Equal(26, landed.Sum(h => h.Damage));
    }

    [Fact]
    public void Huxing_quan_hits_land_on_an_even_beat_slower_than_luohan_quan()
    {
        // Chan: hit - hit - hit, the same gap each time and about 30% slower than 罗汉拳's hit, hithit, hit.
        int[] Beat(string first, int apart)
        {
            var d = DuelOf(Second(), Second(), apart);
            d.One.Loadout.Assign(Slot.Square, T(first));
            var hits = RunTo(d, 150, one: t => t % 2 == 0 && t < 90 ? P(Slot.Square) : None);
            var ticks = hits.Where(h => h.Attacker == Side.One && h.Landed).Select(h => h.Tick).ToArray();
            Assert.True(ticks.Length >= 3, $"{first}: hits at {string.Join(",", ticks)}");
            return new[] { ticks[1] - ticks[0], ticks[2] - ticks[1] };
        }
        var tiger = Beat("huxing_quan_1", 150);
        var luohan = Beat("luohan_quan_1", 110);
        Assert.Equal(tiger[0], tiger[1]);
        Assert.NotEqual(luohan[0], luohan[1]);
        // The old 虎形拳 landed 13 frames apart; 30% slower is 17.
        Assert.Equal(17, tiger[0]);
        Assert.True(luohan[0] > luohan[1], $"luohan gaps {luohan[0]},{luohan[1]}: a pause, then a quick pair");
    }

    [Fact]
    public void Huxing_quan_first_leap_reaches_almost_as_far_as_fengxue_lunges()
    {
        var hu = T("huxing_quan_1");
        var seal = T("fengxue");
        Assert.InRange(hu.AdvanceCm, seal.LungeCm * 3 / 4, seal.LungeCm - 1);
        var d = DuelOf(Second(), Second(), 220);
        var hits = RunTo(d, hu.StartupFrames + hu.ActiveFrames, one: At(0, Slot.Square));
        Assert.Contains(hits, h => h.Attacker == Side.One && h.Landed);
    }

    [Fact]
    public void A_leap_into_a_fighter_at_the_arena_edge_stops_at_min_distance()
    {
        int half = Rules.ArenaWidth / 2;
        var d = new Duel(Rules, Second(), Second(), half - 90, half);
        d.Two.Loadout.Assign(Slot.Square, T("timed_parry"));
        for (int t = 0; t < 40; t++)
        {
            d.Step(t == 0 ? P(Slot.Square) : None, None, MoveInput.None, MoveInput.WalkBack);
            Assert.True(d.DistanceCm >= Rules.MinDistance - 1, $"tick {d.Tick}: {d.DistanceCm} cm");
        }
    }

    // ---------- 狮吼功 ----------

    [Fact]
    public void Shihou_gong_needs_5_Qi_hits_all_round_for_20_and_knocks_down()
    {
        var broke = DuelOf(Second(), Second(), 200, oneQi: 4);
        RunTo(broke, 2, one: At(0, Slot.Triangle));
        Assert.Equal(FighterState.Idle, broke.One.State);

        var d = DuelOf(Second(), Second(), 200, oneQi: 5);
        var hits = RunTo(d, 30, one: At(0, Slot.Triangle));
        Assert.Equal(Rules.QiPerHit, d.One.Qi); // spent all 5, then earned a hit's worth
        var roar = Assert.Single(hits, h => h.Attacker == Side.One);
        Assert.Equal(T("shihou_gong").StartupFrames, roar.Tick);
        Assert.Equal(20, roar.Damage);
        Assert.True(roar.KnockedDown);
        Assert.Equal(FighterState.KnockedDown, d.Two.State);
    }

    [Fact]
    public void A_physical_strike_cant_clash_pure_energy_it_just_hits_the_wind_up()
    {
        // 罗汉拳 hit 1 (13 frames) pressed on tick 5 lands on tick 18, inside 狮吼功's clash window (frames 16-23).
        var d = DuelOf(First(), Second(), 100, twoQi: 5);
        var hits = RunTo(d, 20, one: At(5, Slot.Square), two: At(0, Slot.Triangle));
        var hit = Assert.Single(hits);
        Assert.Equal(ClashOutcome.NoClash, hit.Clash);
        Assert.True(hit.Landed);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
        Assert.Equal(Rules.HpMax, d.One.Hp);
        Assert.Equal(ClashOutcome.NoClash, Clash.Resolve(T("shihou_gong"), T("tiesha_zhang"), 30));
    }

    [Fact]
    public void Two_energy_moves_that_meet_cancel_out_whatever_their_cost()
    {
        var d = DuelOf(Second(), Second(), 150, oneQi: 5, twoQi: 50);
        var hits = RunTo(d, 30, one: At(0, Slot.Triangle), two: At(0, Slot.Triangle));
        var cancel = Assert.Single(hits);
        Assert.Equal(ClashOutcome.EnergyCancel, cancel.Clash);
        Assert.False(cancel.Landed);
        Assert.Equal(Rules.HpMax, d.One.Hp);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
        Assert.Equal(FighterState.Recoil, d.One.State);
        Assert.Equal(FighterState.Recoil, d.Two.State);
        Assert.Equal(FeelEvent.EnergyCancel, FeelCue.Classify(cancel));
    }

    [Fact]
    public void Pure_energy_can_be_blocked_by_huamian_zhang_but_never_perfect_parried()
    {
        // 化绵掌's perfect frames are 5-8; pressed 6 frames before the roar goes off it would be perfect against a strike.
        var parry = T("timed_parry");
        int press = T("shihou_gong").StartupFrames - parry.PerfectParryStart - 1;
        var d = DuelOf(Second(), First(), 150, oneQi: 5);
        var hits = RunTo(d, 30, one: At(0, Slot.Triangle), two: At(press, Slot.L1));
        var hit = Assert.Single(hits);
        Assert.Equal(ParryOutcome.Parried, hit.Parry);
        // Every block is the same (Chan): half of a heavy or energy strike gets through 化绵掌 too.
        Assert.Equal((T("shihou_gong").Damage * Rules.BlockHeavyDamagePct + 50) / 100, hit.Damage);
        Assert.False(hit.KnockedDown);
        Assert.NotEqual(FighterState.Stagger, d.One.State);
        Assert.Equal(ParryOutcome.PerfectParry, Parry.Resolve(parry, parry.PerfectParryStart));
    }

    // ---------- 硬气功 ----------

    [Fact]
    public void Yingqi_gong_held_blocks_light_and_medium_for_stamina_only()
    {
        var d = DuelOf(First(), Second(), 100);
        var hits = RunTo(d, 14, one: At(0, Slot.Square), two: Holding(Slot.L1));
        var hit = Assert.Single(hits);
        Assert.True(hit.Guarded);
        Assert.False(hit.Landed);
        Assert.Equal(0, hit.Damage);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
        Assert.Equal(Rules.StaminaMax - Rules.BlockStaminaMedium, d.Two.Stamina);
        Assert.Equal("yingqi_gong", d.Two.Technique!.Id);
        Assert.Equal(FighterState.Active, d.Two.State);
    }

    [Fact]
    public void Yingqi_gong_takes_half_of_a_heavy_hit_and_no_debuff_or_knockdown()
    {
        var palm = DuelOf(First(), Second(), 100);
        var hit = Assert.Single(RunTo(palm, 40, one: At(0, Slot.Triangle), two: Holding(Slot.L1)));
        Assert.True(hit.Guarded);
        Assert.Equal((T("tiesha_zhang").Damage * Rules.BlockHeavyDamagePct + 50) / 100, hit.Damage);

        var seal = DuelOf(First(), Second(), 100);
        Assert.Contains(RunTo(seal, 40, one: At(0, Slot.Circle), two: Holding(Slot.L1)), h => h.Guarded);
        Assert.False(seal.Two.IsSealed);

        var roar = DuelOf(Second(), Second(), 150, oneQi: 5);
        var r = Assert.Single(RunTo(roar, 30, one: At(0, Slot.Triangle), two: Holding(Slot.L1)));
        Assert.True(r.Guarded);
        Assert.Equal(10, r.Damage);
        Assert.Equal(FighterState.Active, roar.Two.State);
    }

    [Fact]
    public void Yingqi_gong_comes_down_when_let_go_and_cant_stop_a_grab()
    {
        var d = DuelOf(First(), Second(), 300);
        RunTo(d, 20, two: Holding(Slot.L1, until: 21));
        Assert.Equal(FighterState.Active, d.Two.State);
        RunTo(d, 21);
        Assert.Equal(FighterState.Recovery, d.Two.State);
        RunTo(d, 21 + T("yingqi_gong").RecoveryFrames);
        Assert.Equal(FighterState.Idle, d.Two.State);

        var grab = DuelOf(First(), Second(), 90);
        var hits = RunTo(grab, 12, one: At(0, Slot.R2), two: Holding(Slot.L1));
        Assert.Contains(hits, h => h.Grab == GrabPhase.Caught);
        Assert.Equal(FighterState.Grabbed, grab.Two.State);
    }

    [Fact]
    public void Yingqi_gong_only_guards_the_way_it_faced_so_a_strike_from_behind_lands()
    {
        var d = DuelOf(First(), Second(), 100);
        RunTo(d, 3, two: Holding(Slot.L1));
        // One gets round to its back (as 纵云梯 would).
        d.One.Position = d.Two.Position + FloorPoint.FromCm(100, 0);
        var hits = RunTo(d, 30, one: At(4, Slot.Square), two: Holding(Slot.L1, from: -1));
        var hit = Assert.Single(hits);
        Assert.False(hit.Guarded);
        Assert.True(hit.Landed);
    }

    [Fact]
    public void Yingqi_gong_cant_be_raised_while_exhausted()
    {
        var d = DuelOf(First(), Second(), 300);
        d.Two.LoseStamina(Rules.StaminaMax);
        RunTo(d, 2, two: Holding(Slot.L1));
        Assert.Equal(FighterState.Idle, d.Two.State);
    }

    // ---------- 纵云梯 ----------

    [Fact]
    public void Zongyun_ti_jumps_over_the_opponent_and_lands_behind_them()
    {
        var d = DuelOf(Second(), First(), 150);
        RunTo(d, 0, one: At(0, Slot.Cross));
        Assert.Equal(Rules.StaminaMax - T("zongyun_ti").StaminaCost, d.One.Stamina);
        RunTo(d, T("zongyun_ti").TotalFrames + 2);
        Assert.True(d.One.XCm > d.Two.XCm, $"One at {d.One.XCm} should be past Two at {d.Two.XCm}");
        Assert.True(d.DistanceCm >= Rules.MinDistance);
        Assert.Equal(FighterState.Idle, d.One.State);
    }

    [Fact]
    public void Zongyun_ti_slips_a_strike_on_take_off_but_gives_no_just_dodge()
    {
        // Two's 罗汉拳 lands on tick 13; One takes off on tick 10, so tick 13 is in its 6 invincible frames.
        var d = DuelOf(Second(), First(), 130);
        var hits = RunTo(d, 20, one: At(10, Slot.Cross), two: At(0, Slot.Square));
        Assert.Empty(hits);
        Assert.Equal(Rules.HpMax, d.One.Hp);
        Assert.Equal(0, d.One.Qi);
    }

    // ---------- 无相劫指 ----------

    [Fact]
    public void Wuxiang_zhi_is_two_taps_two_strikes_that_lock_movement_when_both_land()
    {
        var finger = T("wuxiang_zhi_1");
        var d = DuelOf(Second(), First(), 90);
        var hits = RunTo(d, 40, one: AtTicks(Slot.Circle, 0, finger.StartupFrames + finger.ActiveFrames));
        var landed = hits.Where(h => h.Attacker == Side.One && h.Landed).ToList();
        Assert.Equal(new[] { "wuxiang_zhi_1", "wuxiang_zhi_2" }, landed.Select(h => h.Technique.Id));
        Assert.Equal(10, landed.Sum(h => h.Damage));
        Assert.Equal(T("wuxiang_zhi_2").CrippleFrames, landed[1].CrippleFrames);
        Assert.True(d.Two.IsCrippled);
        Assert.Equal(FighterState.Idle, d.Two.State);
        Assert.False(d.Two.CanQuickStep);
        Assert.False(d.Two.CanUse(T("qinggong_dash")));
        Assert.True(d.Two.CanUse(T("luohan_quan_1")));

        // Walking at half speed: 10 ticks back covers half of what it would.
        int before = d.DistanceCm;
        for (int i = 0; i < 10; i++) d.Step(None, None, MoveInput.None, MoveInput.WalkBack);
        Assert.InRange(d.DistanceCm - before, Rules.WalkSpeed * Rules.CrippleWalkPct / 100 / 6 - 1, Rules.WalkSpeed * Rules.CrippleWalkPct / 100 / 6 + 1);
    }

    [Fact]
    public void Wuxiang_zhi_blocked_locks_nothing()
    {
        var d = DuelOf(Second(), Second(), 90);
        var hits = RunTo(d, 40, one: AtTicks(Slot.Circle, 0, 5), two: Holding(Slot.L1));
        Assert.Equal(2, hits.Count(h => h.Guarded));
        Assert.False(d.Two.IsCrippled);
    }

    [Fact]
    public void Wuxiang_zhi_tapped_once_strikes_once_and_locks_nothing()
    {
        var d = DuelOf(Second(), First(), 90);
        var hits = RunTo(d, 40, one: At(0, Slot.Circle));
        var hit = Assert.Single(hits);
        Assert.Equal("wuxiang_zhi_1", hit.Technique.Id);
        Assert.False(d.Two.IsCrippled);
    }

    [Fact]
    public void Huamian_zhang_blocks_like_yingqi_gong_half_a_heavy_hit_and_no_debuff()
    {
        // 铁砂掌 lands on frame 36; 化绵掌's plain block frames are 2-4 and 9-18 of its press: press 10 frames before.
        var palm = DuelOf(First(), First(), 100);
        var p = Assert.Single(RunTo(palm, 40, one: At(0, Slot.Triangle), two: At(26, Slot.L1)));
        Assert.Equal(ParryOutcome.Parried, p.Parry);
        Assert.Equal((T("tiesha_zhang").Damage * Rules.BlockHeavyDamagePct + 50) / 100, p.Damage);
        Assert.Equal(Rules.HpMax - p.Damage, palm.Two.Hp);

        // A light 罗汉拳 hit blocked: nothing.
        var fist = DuelOf(First(), First(), 100);
        var f = Assert.Single(RunTo(fist, 14, one: At(0, Slot.Square), two: At(3, Slot.L1)));
        Assert.Equal(ParryOutcome.Parried, f.Parry);
        Assert.Equal(0, f.Damage);

        // A blocked 封穴: no seal.
        var seal = DuelOf(First(), First(), 100);
        var s = Assert.Single(RunTo(seal, 40, one: At(0, Slot.Circle), two: At(20, Slot.L1)));
        Assert.NotEqual(ParryOutcome.NotParried, s.Parry);
        Assert.False(seal.Two.IsSealed);
    }

    [Fact]
    public void A_locked_fighter_cant_dash_out_of_a_combo()
    {
        var d = DuelOf(First(), First(), 90);
        d.Two.Cripple(300);
        RunTo(d, 20, one: At(0, Slot.Square), two: t => t == 14 ? P(Slot.Cross) : None);
        Assert.Equal(FighterState.Hitstun, d.Two.State);
    }

    // ---------- 龙爪手 and the break-out ----------

    [Fact]
    public void Longzhua_shou_strikes_on_each_tap_and_the_third_knocks_down()
    {
        var d = DuelOf(Second(), First(), 90);
        var hits = RunTo(d, 60, one: AtTicks(Slot.R2, 0, 20, 35, 50));
        Assert.Contains(hits, h => h.Grab == GrabPhase.Caught && h.Tick == T("longzhua_shou").StartupFrames);
        var strikes = hits.Where(h => h.Grab == GrabPhase.Strike).ToList();
        Assert.Equal(3, strikes.Count);
        Assert.All(strikes, s => Assert.Equal(T("longzhua_shou").Damage, s.Damage));
        Assert.False(strikes[0].KnockedDown);
        Assert.True(strikes[2].KnockedDown);
        Assert.Equal(Rules.HpMax - 3 * T("longzhua_shou").Damage, d.Two.Hp);
        Assert.Equal(FighterState.KnockedDown, d.Two.State);
    }

    [Fact]
    public void Longzhua_shou_taps_closer_than_the_gap_wait_their_turn()
    {
        var d = DuelOf(Second(), First(), 90);
        var hits = RunTo(d, 40, one: AtTicks(Slot.R2, 0, 20, 28));
        var strikes = hits.Where(h => h.Grab == GrabPhase.Strike).Select(h => h.Tick).ToList();
        Assert.Equal(2, strikes.Count);
        Assert.Equal(20, strikes[0]);
        Assert.Equal(20 + Rules.GrabStrikeGap, strikes[1]);
    }

    [Fact]
    public void Longzhua_shou_with_no_tap_lets_them_slip_free()
    {
        var d = DuelOf(Second(), First(), 90);
        var claw = T("longzhua_shou");
        var hits = RunTo(d, claw.StartupFrames + claw.HoldFrames + 1, one: At(0, Slot.R2));
        var free = Assert.Single(hits, h => h.Grab == GrabPhase.Released);
        Assert.Equal(claw.StartupFrames + claw.HoldFrames, free.Tick);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
        Assert.Equal(FighterState.Idle, d.Two.State);
        Assert.True(d.One.State.IsInMove(), "the grabber plays out the rest of its move: open to a punish");
        Assert.Equal(FeelEvent.GrabBreak, FeelCue.Classify(free));
    }

    [Fact]
    public void A_grab_of_your_own_breaks_out_of_xiao_qinna_shou_before_the_throw()
    {
        var d = DuelOf(First(), First(), 90);
        var hits = RunTo(d, 45, one: At(0, Slot.R2), two: At(20, Slot.R2));
        var broke = Assert.Single(hits, h => h.Grab == GrabPhase.Broken);
        Assert.Equal(Side.Two, broke.Attacker);
        Assert.Equal(20, broke.Tick);
        Assert.DoesNotContain(hits, h => h.Grab == GrabPhase.Thrown);
        Assert.Equal(Rules.HpMax, d.Two.Hp);
        Assert.Equal(Rules.StaminaMax - T("qinna_shou").StaminaCost, d.Two.Stamina);
        // Both step back, Street Fighter style: grab_break_push each, so the gap opens by twice that.
        Assert.InRange(d.DistanceCm, Rules.MinDistance + 2 * Rules.GrabBreakPush - 25, Rules.MinDistance + 2 * Rules.GrabBreakPush + 25);
    }

    [Fact]
    public void Longzhua_shou_can_be_broken_in_its_first_two_windows_but_not_the_third()
    {
        var early = DuelOf(Second(), First(), 90);
        var e = RunTo(early, 40, one: AtTicks(Slot.R2, 0, 20), two: At(30, Slot.R2));
        Assert.Contains(e, h => h.Grab == GrabPhase.Broken);
        Assert.Equal(Rules.HpMax - T("longzhua_shou").Damage, early.Two.Hp);

        var late = DuelOf(Second(), First(), 90);
        var l = RunTo(late, 60, one: AtTicks(Slot.R2, 0, 20, 35, 50), two: At(40, Slot.R2));
        Assert.DoesNotContain(l, h => h.Grab == GrabPhase.Broken);
        Assert.Equal(3, l.Count(h => h.Grab == GrabPhase.Strike));
    }

    // ---------- 易筋经 ----------

    [Fact]
    public void Yijinjing_buffs_all_damage_by_half_for_10_seconds_after_the_channel()
    {
        var qi = T("yijinjing");
        var d = DuelOf(Second(), First(), 300, oneQi: 10);
        int done = qi.StartupFrames + qi.ActiveFrames;
        RunTo(d, done - 1, one: At(0, Slot.R1));
        Assert.False(d.One.IsBuffed);
        RunTo(d, done);
        Assert.True(d.One.IsBuffed);
        Assert.Equal(0, d.One.Qi);
        Assert.Equal(qi.BuffFrames, d.One.BuffFramesLeft);

        // Walk in and land a 罗汉拳 with the first set's loadout swapped onto □.
        d.One.Loadout.Assign(Slot.Square, T("luohan_quan_1"));
        d.One.Position = d.Two.Position - FloorPoint.FromCm(100, 0);
        var hits = RunTo(d, done + 40, one: At(done + 20, Slot.Square));
        var hit = Assert.Single(hits);
        Assert.Equal((T("luohan_quan_1").Damage * 150 + 50) / 100, hit.Damage);

        RunTo(d, done + qi.BuffFrames + 1);
        Assert.False(d.One.IsBuffed);
    }

    [Fact]
    public void Yijinjing_hit_before_it_finishes_gives_no_buff()
    {
        var d = DuelOf(Second(), First(), 100, oneQi: 10);
        RunTo(d, 120, one: At(0, Slot.R1), two: At(40, Slot.Square));
        Assert.False(d.One.IsBuffed);
        Assert.Equal(0, d.One.Qi);
    }

    // ---------- Opponents ----------

    private static BrainSheet Brains => BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv"))).Sheet!;

    [Fact]
    public void The_novice_never_grabs_so_never_breaks_out_and_the_master_breaks_out_most()
    {
        var b = Brains;
        Assert.Equal(0, b.Find("novice")!.Grab);
        Assert.Equal(0, b.Find("novice")!.BreakOut);
        Assert.True(b.Find("monk")!.BreakOut > 0);
        Assert.True(b.Find("master")!.BreakOut > b.Find("monk")!.BreakOut);
    }

    private static int BreakOuts(BrainStyle style, string grab, int seeds)
    {
        int broke = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var player = new Loadout(Book);
            player.Assign(Slot.R2, T(grab));
            var d = DuelOf(player, First(), 90);
            var brain = new Brain(style, seed);
            var all = new List<HitEvent>();
            while (d.Tick < 80 && !d.IsOver)
            {
                var b = brain.Think(d, Side.Two);
                // The grab goes out on tick 0; after that, never a tap, so a 龙爪手 hold just waits.
                all.AddRange(d.Step(d.Tick == 0 ? P(Slot.R2) : None, d.Tick == 0 ? None : b.Slots, MoveInput.None, d.Tick == 0 ? MoveInput.None : b.Move));
            }
            if (all.Any(h => h.Grab == GrabPhase.Broken)) broke++;
        }
        return broke;
    }

    [Fact]
    public void The_master_breaks_out_of_most_grabs_the_novice_of_none()
    {
        int master = BreakOuts(Brains.Find("master")!, "qinna_shou", 40);
        int monk = BreakOuts(Brains.Find("monk")!, "qinna_shou", 40);
        int novice = BreakOuts(Brains.Find("novice")!, "qinna_shou", 40);
        Assert.Equal(0, novice);
        Assert.InRange(master, 24, 40);
        Assert.True(monk < master, $"monk {monk}, master {master}");
        Assert.True(BreakOuts(Brains.Find("master")!, "longzhua_shou", 20) > 10);
    }

    private static BrainStyle Style(int grab = 0, int parry = 0, int aggression = 0, int reaction = 12) =>
        new("t", "t", reaction, 6, aggression, 0, 100, parry, 0, 0, 0, 0, 0, 0, 0, grab, 0, 0);

    [Fact]
    public void A_brain_with_longzhua_shou_taps_out_all_three_strikes()
    {
        var mine = new Loadout(Book);
        mine.Assign(Slot.R2, T("longzhua_shou"));
        var d = DuelOf(new Loadout(Book), mine, 90);
        var brain = new Brain(Style(grab: 100), 3);
        var all = new List<HitEvent>();
        while (d.Tick < 300 && !d.IsOver)
        {
            var b = brain.Think(d, Side.Two);
            all.AddRange(d.Step(None, b.Slots, MoveInput.None, b.Move));
            if (all.Any(h => h.KnockedDown)) break;
        }
        Assert.Equal(3, all.Count(h => h.Grab == GrabPhase.Strike));
        Assert.Equal(FighterState.KnockedDown, d.One.State);
    }

    [Fact]
    public void A_brain_with_yingqi_gong_holds_it_up_through_a_strike_it_sees_coming()
    {
        var mine = new Loadout(Book);
        mine.Assign(Slot.L1, T("yingqi_gong"));
        var d = DuelOf(First(), mine, 100);
        var brain = new Brain(Style(parry: 100), 5);
        var all = new List<HitEvent>();
        while (d.Tick < 60 && !d.IsOver)
        {
            var b = brain.Think(d, Side.Two);
            all.AddRange(d.Step(d.Tick == 0 ? P(Slot.Triangle) : None, b.Slots, MoveInput.None, b.Move));
        }
        var hit = Assert.Single(all);
        Assert.True(hit.Guarded);
        // Once the palm has passed it lets go.
        Assert.False(d.Two.State == FighterState.Active && d.Two.Technique!.IsGuard);
    }

    [Fact]
    public void Loadout_replace_swaps_a_move_for_its_alternative_on_the_same_button()
    {
        var l = First();
        Assert.True(l.Replace(T("luohan_quan_1"), T("huxing_quan_1")));
        Assert.Equal("huxing_quan_1", l[Slot.Square]!.Id);
        Assert.False(l.Replace(T("luohan_quan_1"), T("huxing_quan_1")));
    }

    [Theory]
    [InlineData("novice")]
    [InlineData("monk")]
    [InlineData("master")]
    public void Brains_fight_out_whole_duels_with_the_second_set_and_with_both_sets(string id)
    {
        var style = Brains.Find(id)!;
        for (int seed = 1; seed <= 6; seed++)
        {
            var one = seed % 2 == 0 ? Second() : First();
            var d = DuelOf(one, Second(), 300);
            var a = new Brain(style, seed);
            var b = new Brain(Brains.Find("master")!, seed + 100);
            while (d.Tick < 60 * 90 && !d.IsOver)
            {
                var x = a.Think(d, Side.One);
                var y = b.Think(d, Side.Two);
                d.Step(x.Slots, y.Slots, x.Move, y.Move);
                Assert.True(d.DistanceCm >= Rules.MinDistance - 2 || d.One.IsAirborne || d.Two.IsAirborne,
                    $"tick {d.Tick}: {d.DistanceCm} cm apart with nobody in the air");
            }
            Assert.True(d.Hits.Count(h => h.Landed) > 5, $"seed {seed}: only {d.Hits.Count(h => h.Landed)} hits landed");
        }
    }
}
