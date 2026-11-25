using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Stage 2 step 1, the feel kit: every strike is sorted into a row of the feel sheet, and the fight
/// freezes for that row's hit-stop without changing any frame data.
/// </summary>
public class FeelTests
{
    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    // A 2-hit string: hit 1 lands on frame 5, chain window is its first 8 recovery frames.
    private static readonly Technique Jab2 = Make.Move("jab2", startup: 5, active: 3, recovery: 12, window: 0, perfect: 0,
        damage: 6, hitstun: 30, stamina: 0);
    private static readonly Technique Jab1 = Make.Move("jab1", startup: 5, active: 3, recovery: 12, window: 0, perfect: 0,
        damage: 6, hitstun: 30, stamina: 0, next: "jab2", chain: 8);
    private static readonly Technique Heavy = Make.Move("heavy", Weight.Heavy, startup: 5, active: 3, recovery: 12, window: 0,
        perfect: 0, damage: 10, hitstun: 30, stamina: 0);
    private static readonly TechniqueBook Book = new(new[] { Jab1, Jab2, Heavy });

    private static readonly Rules R = RulesWith();

    private static Rules RulesWith()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("qi_max", "100"), ("qi_start", "0"), ("walk_speed", "120")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    /// <summary>A feel sheet where hit freezes 5, heavy 8, KO 20 and everything else 3.</summary>
    private static FeelSheet Sheet()
    {
        var text = "event,hitstop,shake,push,spark,sound\n";
        foreach (var name in FeelSheet.EventNames.Keys)
        {
            int stop = name == "hit" ? 5 : name == "heavy_hit" ? 8 : name == "ko" ? 20 : 3;
            text += $"{name},{stop},1,0,10,smack\n";
        }
        var r = FeelCsv.Parse(text);
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!;
    }

    private static Loadout Holding(Technique square)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, square);
        return l;
    }

    private static Duel NewDuel(Technique one, Technique two, int hp = 1000) =>
        new(RulesHp(hp), Holding(one), Holding(two), 0, 100);

    private static Rules RulesHp(int hp)
    {
        if (hp == 1000) return R;
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("input_buffer", "6"), ("hp_max", hp.ToString()),
            ("qi_max", "100"), ("qi_start", "0")));
        return r.Rules!;
    }

    // ---------- Hit-stop ----------

    [Fact]
    public void A_hit_freezes_the_fight_for_its_hitstop()
    {
        var clock = new FeelClock(NewDuel(Jab1, Jab1), Sheet());
        int hitAt = -1;
        for (int t = 0; t < 30 && hitAt < 0; t++)
            if (clock.Step(t == 0 ? Square : None, None).Count > 0) hitAt = t;

        Assert.Equal(5, hitAt); // lands on frame 5, like without the kit
        Assert.Equal(FeelEvent.Hit, Assert.Single(clock.Cues).Event);
        Assert.Equal(5, clock.FrozenFor);

        int duelTick = clock.Duel.Tick;
        int stunLeft = clock.Duel.Two.StunFramesLeft;
        for (int i = 0; i < 5; i++)
        {
            Assert.True(clock.IsFrozen);
            Assert.Empty(clock.Step(None, None));
            Assert.Equal(duelTick, clock.Duel.Tick);
            Assert.Equal(stunLeft, clock.Duel.Two.StunFramesLeft);
        }
        Assert.False(clock.IsFrozen);
        clock.Step(None, None);
        Assert.Equal(duelTick + 1, clock.Duel.Tick);
    }

    [Fact]
    public void Hit_stop_changes_no_frame_data_only_adds_still_ticks()
    {
        // The same string with and without the kit: the same hits on the same duel ticks.
        var plain = NewDuel(Jab1, Jab1);
        var clock = new FeelClock(NewDuel(Jab1, Jab1), Sheet());
        // Press for hit 1 at duel tick 0, and for hit 2 on duel tick 8 (the first chain frame).
        for (int i = 0; i < 200; i++)
        {
            var press = plain.Tick == 0 || plain.Tick == 8 ? Square : None;
            plain.Step(press, None);
        }
        for (int i = 0; i < 200; i++)
        {
            var press = !clock.IsFrozen && (clock.Duel.Tick == 0 || clock.Duel.Tick == 8) ? Square : None;
            clock.Step(press, None);
        }

        Assert.Equal(2, plain.Hits.Count);
        Assert.Equal(plain.Hits.Select(h => h.Tick), clock.Duel.Hits.Select(h => h.Tick));
        Assert.Equal(plain.Two.Hp, clock.Duel.Two.Hp);
        // 200 clock ticks, two 5-tick freezes: the duel ran 10 fewer ticks.
        Assert.Equal(200, clock.Ticks);
        Assert.Equal(190, clock.Duel.Tick);
    }

    [Fact]
    public void A_press_during_the_freeze_is_kept_so_the_chain_still_happens()
    {
        var clock = new FeelClock(NewDuel(Jab1, Jab1), Sheet());
        bool pressedInFreeze = false;
        for (int t = 0; t < 120; t++)
        {
            var press = t == 0 ? Square : None;
            if (clock.IsFrozen && !pressedInFreeze)
            {
                press = Square;
                pressedInFreeze = true;
            }
            clock.Step(press, None);
        }

        Assert.True(pressedInFreeze);
        Assert.Equal(new[] { "jab1", "jab2" }, clock.Duel.Hits.Select(h => h.Technique.Id));
    }

    [Fact]
    public void Nobody_walks_during_the_freeze()
    {
        var clock = new FeelClock(NewDuel(Jab1, Jab1), Sheet());
        for (int t = 0; t <= 5; t++) clock.Step(t == 0 ? Square : None, None);
        Assert.True(clock.IsFrozen);
        int pos = clock.Duel.Two.XCm;
        clock.Step(None, None, MoveInput.None, MoveInput.WalkBack);
        Assert.Equal(pos, clock.Duel.Two.XCm);
    }

    [Fact]
    public void A_trade_freezes_for_the_longer_hitstop()
    {
        // A medium jab and a heavy hit land on the same tick: hit (5) and heavy_hit (8).
        var clock = new FeelClock(NewDuel(Jab1, Heavy), Sheet());
        for (int t = 0; t <= 5; t++) clock.Step(t == 0 ? Square : None, t == 0 ? Square : None);

        Assert.Equal(2, clock.Cues.Count);
        Assert.Contains(clock.Cues, c => c.Event == FeelEvent.Hit);
        Assert.Contains(clock.Cues, c => c.Event == FeelEvent.HeavyHit);
        Assert.Equal(8, clock.FrozenFor);
    }

    [Fact]
    public void With_hit_stop_off_strikes_still_give_cues_but_nothing_freezes()
    {
        var clock = new FeelClock(NewDuel(Jab1, Jab1), Sheet()) { HitStopOn = false };
        for (int t = 0; t <= 5; t++) clock.Step(t == 0 ? Square : None, None);
        Assert.Single(clock.Cues);
        Assert.False(clock.IsFrozen);
    }

    [Fact]
    public void The_finishing_blow_is_a_ko()
    {
        var clock = new FeelClock(NewDuel(Jab1, Jab1, hp: 6), Sheet());
        for (int t = 0; t <= 5; t++) clock.Step(t == 0 ? Square : None, None);
        Assert.Equal(FeelEvent.Ko, Assert.Single(clock.Cues).Event);
        Assert.Equal(20, clock.FrozenFor);
        Assert.True(clock.Duel.IsOver);
    }

    // ---------- Which row a strike uses ----------

    private static HitEvent Strike(Technique t, int hpAfter = 50, ParryOutcome parry = ParryOutcome.NotParried,
        ClashOutcome clash = ClashOutcome.NoClash, bool empowered = false, bool armored = false) =>
        new(0, Side.One, t, 5, 10, hpAfter, parry: parry, clash: clash,
            clashEffect: clash == ClashOutcome.NoClash ? null : Clash.Effect(clash, R), empowered: empowered, defenderArmored: armored);

    [Fact]
    public void Each_kind_of_strike_gets_its_own_row()
    {
        Assert.Equal(FeelEvent.Hit, FeelCue.Classify(Strike(Jab1)));
        Assert.Equal(FeelEvent.HeavyHit, FeelCue.Classify(Strike(Heavy)));
        Assert.Equal(FeelEvent.EmpoweredHit, FeelCue.Classify(Strike(Heavy, empowered: true)));
        Assert.Equal(FeelEvent.ArmorHit, FeelCue.Classify(Strike(Heavy, armored: true)));
        Assert.Equal(FeelEvent.Block, FeelCue.Classify(Strike(Jab1, parry: ParryOutcome.Parried)));
        Assert.Equal(FeelEvent.PerfectParry, FeelCue.Classify(Strike(Jab1, parry: ParryOutcome.PerfectParry)));
        Assert.Equal(FeelEvent.ClashRecoil, FeelCue.Classify(Strike(Jab1, clash: ClashOutcome.BothRecoil)));
        Assert.Equal(FeelEvent.ClashBreak, FeelCue.Classify(Strike(Heavy, clash: ClashOutcome.StrikerWins)));
        Assert.Equal(FeelEvent.ClashLost, FeelCue.Classify(Strike(Jab1, clash: ClashOutcome.TargetWins)));
        Assert.Equal(FeelEvent.PerfectClash, FeelCue.Classify(Strike(Jab1, clash: ClashOutcome.PerfectClash)));
        Assert.Equal(FeelEvent.Ko, FeelCue.Classify(Strike(Jab1, hpAfter: 0)));
        Assert.Equal(FeelEvent.Ko, FeelCue.Classify(Strike(Heavy, hpAfter: 0, clash: ClashOutcome.StrikerWins)));
    }

    [Fact]
    public void A_parried_or_clashed_strike_on_zero_hp_is_not_a_ko()
    {
        // Only a strike that lands can finish the fight.
        Assert.Equal(FeelEvent.Block, FeelCue.Classify(Strike(Jab1, hpAfter: 0, parry: ParryOutcome.Parried)));
        Assert.Equal(FeelEvent.ClashRecoil, FeelCue.Classify(Strike(Jab1, hpAfter: 0, clash: ClashOutcome.BothRecoil)));
    }

    // ---------- The sheet ----------

    private static string SheetText(params (string eventName, string row)[] overrides)
    {
        var text = "event,hitstop,shake,push,spark,sound,note\n";
        foreach (var name in FeelSheet.EventNames.Keys)
        {
            var row = $"{name},4,2,0,20,smack,test";
            foreach (var (e, r) in overrides) if (e == name) row = r;
            if (row.Length > 0) text += row + "\n";
        }
        return text;
    }

    [Fact]
    public void A_full_sheet_loads()
    {
        var r = FeelCsv.Parse(SheetText(("ko", "ko,20,10,40,90,gong,the end")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var ko = r.Sheet![FeelEvent.Ko];
        Assert.Equal(20, ko.HitStopFrames);
        Assert.Equal(10, ko.ShakeCm);
        Assert.Equal(40, ko.PushCm);
        Assert.Equal(90, ko.SparkCm);
        Assert.Equal(FeelSound.Gong, ko.Sound);
    }

    [Theory]
    [InlineData("hit,31,2,0,20,smack", "hitstop): must be a whole number from 0 to 30")]
    [InlineData("hit,-1,2,0,20,smack", "hitstop): must be a whole number from 0 to 30")]
    [InlineData("hit,4,51,0,20,smack", "shake): must be a whole number from 0 to 50")]
    [InlineData("hit,4,2,101,20,smack", "push): must be a whole number from 0 to 100")]
    [InlineData("hit,4,2,0,201,smack", "spark): must be a whole number from 0 to 200")]
    [InlineData("hit,4,2,0,abc,smack", "spark): must be a whole number from 0 to 200, got 'abc'")]
    [InlineData("hit,4,2,0,20,boom", "unknown sound 'boom'")]
    [InlineData("hit,4,2,0,20,3", "unknown sound '3'")]
    [InlineData("", "missing event 'hit'")]
    public void Bad_rows_are_reported(string hitRow, string expected)
    {
        var r = FeelCsv.Parse(SheetText(("hit", hitRow)));
        Assert.False(r.Ok);
        Assert.Null(r.Sheet);
        Assert.Contains(r.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Unknown_and_duplicate_events_are_reported()
    {
        var r = FeelCsv.Parse(SheetText() + "kick,4,2,0,20,smack\nhit,4,2,0,20,smack\n");
        Assert.Contains(r.Errors, e => e.Contains("unknown event 'kick'"));
        Assert.Contains(r.Errors, e => e.Contains("duplicate event 'hit'"));
    }

    [Fact]
    public void A_missing_column_is_reported()
    {
        var r = FeelCsv.Parse("event,hitstop,shake,push,spark\nhit,4,2,0,20\n");
        Assert.Contains(r.Errors, e => e.Contains("missing column 'sound'"));
    }

    [Fact]
    public void Sound_names_ignore_case()
    {
        var r = FeelCsv.Parse(SheetText(("block", "block,3,1,0,20,BLOCK")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(FeelSound.Block, r.Sheet![FeelEvent.Block].Sound);
    }

    // ---------- The real sheet ----------

    private static FeelSheet RealSheet()
    {
        var r = FeelCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("feel.csv")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Sheet!;
    }

    [Fact]
    public void Feel_sheet_passes_the_check()
    {
        RealSheet();
    }

    [Fact]
    public void The_clash_is_the_showpiece()
    {
        // Brief: "Make the clash the showpiece". A perfect clash freezes longer than any hit or parry;
        // only the KO is bigger.
        var s = RealSheet();
        foreach (FeelEvent e in Enum.GetValues(typeof(FeelEvent)))
        {
            if (e == FeelEvent.PerfectClash || e == FeelEvent.Ko) continue;
            Assert.True(s[FeelEvent.PerfectClash].HitStopFrames > s[e].HitStopFrames, $"perfect_clash should freeze longer than {e}");
        }
        Assert.True(s[FeelEvent.Ko].HitStopFrames >= s[FeelEvent.PerfectClash].HitStopFrames);
    }

    [Fact]
    public void Heavier_hits_freeze_longer()
    {
        var s = RealSheet();
        Assert.True(s[FeelEvent.Hit].HitStopFrames < s[FeelEvent.HeavyHit].HitStopFrames);
        Assert.True(s[FeelEvent.HeavyHit].HitStopFrames < s[FeelEvent.EmpoweredHit].HitStopFrames);
        Assert.True(s[FeelEvent.Block].HitStopFrames < s[FeelEvent.PerfectParry].HitStopFrames);
    }

    [Fact]
    public void A_just_dodge_slows_the_game_with_a_light_whoosh_and_no_freeze()
    {
        var row = RealSheet()[FeelEvent.JustDodge];
        Assert.Equal(FeelSound.Whoosh, row.Sound);
        Assert.True(row.SlowFrames > 0);
        Assert.Equal(0, row.HitStopFrames);
    }

    [Fact]
    public void A_just_dodge_is_classified_as_one()
    {
        var jab = Make.Move("jab");
        var dodge = new HitEvent(0, Side.Two, jab, 0, 0, 100, justDodged: true);
        Assert.Equal(FeelEvent.JustDodge, FeelCue.Classify(dodge));
        Assert.False(dodge.Landed);
    }

    [Fact]
    public void The_slow_column_is_optional_and_checked()
    {
        var ok = FeelCsv.Parse(SheetText());
        Assert.True(ok.Ok, string.Join("\n", ok.Errors));
        Assert.Equal(0, ok.Sheet![FeelEvent.Hit].SlowFrames);
    }
}
