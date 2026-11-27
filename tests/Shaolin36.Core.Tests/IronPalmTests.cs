using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// 铁砂掌 (Chan's test technique): one heavy hit, and a Qi-empowered version. If the fighter has
/// empower_qi (30) Qi when it starts, it spends it, deals empower_damage_pct (200%) damage and has
/// hyper armor through startup and active: hits still hurt but don't interrupt it.
/// </summary>
public class IronPalmTests
{
    // Startup 20 (frames 0-19, clash window 12-19), active 20-23, recovery 24-43. 15 damage, 40 hitstun.
    private static readonly Technique Palm = new("palm", "palm", Category.Waigong, Weight.Heavy, false,
        20, 4, 20, 8, 2, 20, 0, 15, true, 40, 130, "", 0, 0, empowerQi: 30, empowerDamagePct: 200, empowerArmor: true);
    // A quick long jab: startup 5, lands on frame 5, 6 damage, 18 hitstun, reaches 200 cm (past the palm's 130).
    private static readonly Technique Jab = Make.Move("jab", startup: 5, active: 3, recovery: 10, window: 0, perfect: 0,
        damage: 6, hitstun: 18, reach: 200, stamina: 0);
    private static readonly Technique Parry = Make.ParryMove(); // window frames 3-12, perfect 3-6
    private static readonly TechniqueBook Book = new(new[] { Palm, Jab, Parry });

    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    private static readonly Rules R = RulesWith();

    private static Rules RulesWith()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("qi_max", "100"), ("qi_start", "0"), ("qi_per_hit", "2"), ("parry_perfect_qi", "10"), ("clash_perfect_qi", "10"),
            ("parry_perfect_stagger", "30")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique square)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, square);
        return l;
    }

    /// <summary>One throws the palm on tick 0 with <paramref name="qi"/> Qi; Two presses its move on the given ticks.</summary>
    private static Duel Run(int qi, Technique two, int untilTick, params int[] twoPressTicks) =>
        RunAt(100, qi, two, untilTick, twoPressTicks);

    private static Duel RunAt(int distance, int qi, Technique two, int untilTick, params int[] twoPressTicks)
    {
        var d = new Duel(R, Holding(Palm), Holding(two), 0, distance, oneQi: qi);
        for (int t = 0; t <= untilTick; t++)
            d.Step(t == 0 ? Square : None, System.Array.IndexOf(twoPressTicks, t) >= 0 ? Square : None);
        return d;
    }

    // ---------- Empowering ----------

    [Fact]
    public void With_enough_qi_the_move_is_empowered_and_spends_it()
    {
        var f = new Fighter(Holding(Palm), 100, 0, 0, null, maxQi: 100, qi: 45);
        f.Tick(Square);
        Assert.Same(Palm, f.Technique);
        Assert.True(f.Empowered);
        Assert.True(f.HasArmor);
        Assert.Equal(15, f.Qi);
    }

    [Fact]
    public void Exactly_enough_qi_is_enough()
    {
        var f = new Fighter(Holding(Palm), 100, 0, 0, null, maxQi: 100, qi: 30);
        f.Tick(Square);
        Assert.True(f.Empowered);
        Assert.Equal(0, f.Qi);
    }

    [Fact]
    public void Without_enough_qi_it_is_the_plain_move_and_keeps_the_qi()
    {
        var f = new Fighter(Holding(Palm), 100, 0, 0, null, maxQi: 100, qi: 29);
        f.Tick(Square);
        Assert.Same(Palm, f.Technique);
        Assert.False(f.Empowered);
        Assert.False(f.HasArmor);
        Assert.Equal(29, f.Qi);
    }

    [Fact]
    public void Empowered_ends_with_the_move()
    {
        var f = new Fighter(Holding(Palm), 100, 0, 0, null, maxQi: 100, qi: 30);
        f.Tick(Square);
        for (int i = 0; i < Palm.TotalFrames; i++) f.Tick(None);
        Assert.True(f.CanAct);
        Assert.False(f.Empowered);
    }

    // ---------- Damage ----------

    [Fact]
    public void The_plain_palm_deals_its_damage()
    {
        var d = Run(0, Jab, 20);
        var hit = Assert.Single(d.Hits);
        Assert.False(hit.Empowered);
        Assert.Equal(15, hit.Damage);
        Assert.Equal(40, hit.HitstunFrames);
    }

    [Fact]
    public void The_empowered_palm_deals_double_damage()
    {
        var d = Run(30, Jab, 20);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Empowered);
        Assert.Equal(30, hit.Damage);
        Assert.Equal(R.HpMax - 30, d.Two.Hp);
        Assert.Equal(2, d.One.Qi); // 30 spent, then +2 for the hit
    }

    // ---------- Hyper armor ----------

    [Fact]
    public void Without_armor_a_hit_in_the_wind_up_interrupts_the_palm()
    {
        var d = Run(0, Jab, 20, 0); // jab lands on tick 5, palm frame 5 (outside its clash window)
        var hit = Assert.Single(d.Hits);
        Assert.Same(Jab, hit.Technique);
        Assert.Equal(18, hit.HitstunFrames);
        Assert.Null(d.One.Technique); // the palm was cancelled and never lands
    }

    [Fact]
    public void Armor_takes_the_damage_but_the_palm_carries_on_and_lands()
    {
        var d = Run(30, Jab, 20, 0);
        var jab = d.Hits[0];
        Assert.Same(Jab, jab.Technique);
        Assert.True(jab.DefenderArmored);
        Assert.True(jab.Landed);
        Assert.Equal(6, jab.Damage);
        Assert.Equal(0, jab.HitstunFrames);
        Assert.Equal(R.HpMax - 6, d.One.Hp);

        // The palm still lands on tick 20, at double damage.
        Assert.Equal(2, d.Hits.Count);
        var palm = d.Hits[1];
        Assert.Same(Palm, palm.Technique);
        Assert.Equal(20, palm.Tick);
        Assert.Equal(30, palm.Damage);
    }

    [Fact]
    public void Armor_ends_after_the_active_frames()
    {
        // 150 cm apart, so the palm whiffs; the jab pressed on tick 19 lands on tick 24, the palm's first recovery frame.
        var d = RunAt(150, 30, Jab, 24, 19);
        var jab = d.Hits.Single(h => h.Technique == Jab);
        Assert.False(jab.DefenderArmored);
        Assert.Equal(18, jab.HitstunFrames);
        Assert.Equal(FighterState.Hitstun, d.One.State);
    }

    [Fact]
    public void A_perfect_parry_still_staggers_an_armored_palm()
    {
        var d = Run(30, Parry, 20, 17); // parry frame 3 on tick 20: perfect
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(0, hit.Damage);
        Assert.Equal(FighterState.Stagger, d.One.State);
    }

    // ---------- The sheet ----------

    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry";

    [Fact]
    public void Sheets_without_the_empower_columns_still_load()
    {
        var r = TechniqueCsv.Parse(Header + "\na,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.False(r.Techniques[0].CanEmpower);
        Assert.Equal(100, r.Techniques[0].EmpowerDamagePct);
    }

    [Fact]
    public void Reads_the_empower_columns()
    {
        var r = TechniqueCsv.Parse(Header + ",empower_qi,empower_damage_pct,empower_armor\n" +
                                   "p,P,waigong,heavy,false,36,4,36,8,2,20,0,15,true,40,130,,0,0,30,200,yes\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var t = r.Techniques[0];
        Assert.Equal(30, t.EmpowerQi);
        Assert.Equal(200, t.EmpowerDamagePct);
        Assert.True(t.EmpowerArmor);
    }

    [Theory]
    [InlineData("0,200,false")]
    [InlineData("0,100,true")]
    [InlineData("30,0,true")]
    public void Rejects_an_empowered_version_that_cannot_happen(string empower)
    {
        var r = TechniqueCsv.Parse(Header + ",empower_qi,empower_damage_pct,empower_armor\n" +
                                   "p,P,waigong,heavy,false,36,4,36,8,2,20,0,15,true,40,130,,0,0," + empower + "\n");
        Assert.False(r.Ok);
        Assert.Contains("line 2 (p)", r.Errors[0]);
    }

    [Fact]
    public void The_real_sheet_has_tiesha_zhang()
    {
        var palm = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques
            .Single(t => t.Id == "tiesha_zhang");
        Assert.Equal(Weight.Heavy, palm.Weight);
        Assert.Equal(36, palm.StartupFrames);
        Assert.Equal(3, palm.ActiveFrames);
        Assert.Equal(32, palm.RecoveryFrames);
        Assert.Equal(20, palm.StaminaCost);
        Assert.Equal(15, palm.Damage);
        Assert.Equal(30, palm.EmpowerQi);
        Assert.Equal(200, palm.EmpowerDamagePct);
        Assert.True(palm.EmpowerArmor);
        Assert.False(palm.HasNext);
    }

    [Fact]
    public void Tiesha_zhang_stands_its_ground_through_the_wind_up_and_steps_in_with_the_palm()
    {
        var sheet = TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques;
        var rules = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        var book = new TechniqueBook(sheet);
        var palm = book.Find("tiesha_zhang")!;
        Assert.Equal(0, palm.AdvanceCm);
        Assert.Equal(20, palm.LungeCm);
        var loadout = new Loadout(book);
        loadout.Assign(Slot.Square, palm);
        var d = new Duel(rules, loadout, new Loadout(book), 0, 300); // out of reach, so nothing stops the step
        for (int t = 0; t < palm.StartupFrames; t++)
        {
            d.Step(t == 0 ? Square : None, None);
            Assert.True(d.One.XCm == 0, $"wind-up frame {t + 1}: moved to {d.One.XCm} cm");
        }
        for (int t = 0; t < palm.ActiveFrames; t++)
        {
            int x = d.One.XCm;
            d.Step(None, None);
            Assert.True(d.One.XCm > x, $"hit frame {t + 1}: didn't step in");
        }
        Assert.Equal(palm.LungeCm, d.One.XCm);
    }
}
