using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Stage 1 step 7, last part: Qi. Fighters start a duel on qi_start (or what they carried in),
/// gain qi_per_hit for each strike that deals damage, parry_perfect_qi for a perfect parry and
/// clash_perfect_qi for a perfect clash, never above qi_max. Techniques with a Qi cost need that
/// much Qi to start and pay it when they do.
///
/// Duel scenarios work like <see cref="ClashParryDuelTests"/>: One presses on tick 0 and its hit
/// lands on tick 10; Two's press tick sets what Two is doing then.
/// </summary>
public class QiTests
{
    // Startup 10 (clash window frames 6-9, perfect 6-7), active 10-12, recovery 12.
    private static readonly Technique Punch = Make.Move("punch", startup: 10, active: 3, recovery: 12, window: 4, perfect: 2,
        hitstun: 18, reach: 120, damage: 7, next: "punch2", chain: 8);
    private static readonly Technique Punch2 = Make.Move("punch2", startup: 10, damage: 7);
    private static readonly Technique Heavy = Make.Move("heavy", Weight.Heavy, startup: 10, damage: 12, hitstun: 18);
    private static readonly Technique Light = Make.Move("light", Weight.Light, startup: 10, damage: 4);
    private static readonly Technique Parry = Make.ParryMove(); // window frames 3-12, perfect 3-6
    private static readonly Technique QiStrike = Make.Move("qistrike", category: Category.Dianxue, qi: 30, stamina: 0);
    private static readonly TechniqueBook Book = new(new[] { Punch, Punch2, Heavy, Light, Parry, QiStrike });

    private static readonly SlotInput None = SlotInput.None;
    private static readonly SlotInput Square = SlotInput.Press(Slot.Square);

    private static readonly Rules R = RulesWith();

    private static Rules RulesWith(params (string, string)[] extra)
    {
        var all = new List<(string, string)>
        {
            ("exhausted_until", "50"), ("input_buffer", "6"), ("hp_max", "1000"),
            ("qi_max", "100"), ("qi_start", "0"), ("qi_per_hit", "2"), ("parry_perfect_qi", "10"), ("clash_perfect_qi", "10"),
        };
        all.AddRange(extra);
        var r = RulesCsv.Parse(Make.RulesText(all.ToArray()));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Rules!;
    }

    private static Loadout Holding(Technique square)
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, square);
        return l;
    }

    /// <summary>One presses Square on tick 0, Two on <paramref name="twoPressTick"/> (-1: never); runs to tick 10.</summary>
    private static Duel Run(Technique one, Technique two, int twoPressTick, Rules? rules = null, int? oneQi = null)
    {
        var d = new Duel(rules ?? R, Holding(one), Holding(two), 0, 100, oneQi);
        for (int t = 0; t <= 10; t++)
            d.Step(t == 0 ? Square : None, t == twoPressTick ? Square : None);
        return d;
    }

    // ---------- Starting Qi ----------

    [Fact]
    public void A_duel_starts_both_fighters_on_qi_start()
    {
        var d = Duel.Start(RulesWith(("qi_start", "20")), Holding(Punch), Holding(Punch));
        Assert.Equal(100, d.One.MaxQi);
        Assert.Equal(20, d.One.Qi);
        Assert.Equal(20, d.Two.Qi);
    }

    [Fact]
    public void A_duel_can_start_with_qi_carried_over()
    {
        var d = Duel.Start(R, Holding(Punch), Holding(Punch), oneQi: 45);
        Assert.Equal(45, d.One.Qi);
        Assert.Equal(0, d.Two.Qi);
    }

    [Fact]
    public void Carried_qi_cannot_be_more_than_qi_max()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Duel.Start(R, Holding(Punch), Holding(Punch), oneQi: 101));
    }

    // ---------- Gains ----------

    [Fact]
    public void Landing_a_hit_gives_qi_per_hit()
    {
        var d = Run(Punch, Punch, twoPressTick: -1);
        var hit = Assert.Single(d.Hits);
        Assert.True(hit.Landed);
        Assert.Equal(2, hit.AttackerQiGained);
        Assert.Equal(0, hit.DefenderQiGained);
        Assert.Equal(2, d.One.Qi);
        Assert.Equal(0, d.Two.Qi);
    }

    [Fact]
    public void A_whole_string_gives_qi_for_every_hit()
    {
        var d = new Duel(R, Holding(Punch), Holding(Punch), 0, 100);
        // Punch lands on tick 10, chain on tick 13 (first recovery frame), punch2 lands on tick 23.
        for (int t = 0; t <= 23; t++) d.Step(t == 0 || t == 13 ? Square : None, None);
        Assert.Equal(2, d.Hits.Count);
        Assert.Equal(4, d.One.Qi);
    }

    [Fact]
    public void A_perfect_parry_gives_the_defender_parry_perfect_qi()
    {
        var d = Run(Punch, Parry, twoPressTick: 7); // parry frame 3 on tick 10: perfect
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.PerfectParry, hit.Parry);
        Assert.Equal(10, hit.DefenderQiGained);
        Assert.Equal(0, hit.AttackerQiGained);
        Assert.Equal(10, d.Two.Qi);
        Assert.Equal(0, d.One.Qi);
    }

    [Fact]
    public void A_plain_block_gives_no_qi()
    {
        var d = Run(Punch, Parry, twoPressTick: 2); // parry frame 8: plain block
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ParryOutcome.Parried, hit.Parry);
        Assert.Equal(0, d.One.Qi);
        Assert.Equal(0, d.Two.Qi);
    }

    [Fact]
    public void A_perfect_clash_gives_the_striker_clash_perfect_qi()
    {
        var d = Run(Punch, Punch, twoPressTick: 4); // Two on frame 6, the first perfect-clash frame
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.PerfectClash, hit.Clash);
        Assert.Equal(10, hit.AttackerQiGained);
        Assert.Equal(10, d.One.Qi);
        Assert.Equal(0, d.Two.Qi);
    }

    [Fact]
    public void Winning_a_clash_with_a_heavier_move_counts_as_a_landed_hit()
    {
        var d = Run(Heavy, Punch, twoPressTick: 2); // Two on frame 8: normal clash, heavy wins
        var hit = Assert.Single(d.Hits);
        Assert.Equal(ClashOutcome.StrikerWins, hit.Clash);
        Assert.True(hit.Landed);
        Assert.Equal(2, d.One.Qi);
    }

    [Theory]
    [InlineData("same weight")]
    [InlineData("lighter")]
    public void Recoil_and_lost_clashes_give_no_qi(string which)
    {
        var d = Run(which == "lighter" ? Light : Punch, Punch, twoPressTick: 2);
        var hit = Assert.Single(d.Hits);
        Assert.Equal(which == "lighter" ? ClashOutcome.TargetWins : ClashOutcome.BothRecoil, hit.Clash);
        Assert.Equal(0, d.One.Qi);
        Assert.Equal(0, d.Two.Qi);
    }

    [Fact]
    public void Qi_never_goes_above_qi_max()
    {
        var d = Run(Punch, Parry, twoPressTick: 7, rules: RulesWith(("qi_start", "95")));
        var hit = Assert.Single(d.Hits);
        Assert.Equal(5, hit.DefenderQiGained); // only room for 5
        Assert.Equal(100, d.Two.Qi);
    }

    // ---------- Spending ----------

    [Fact]
    public void A_qi_technique_needs_enough_qi_to_start()
    {
        var f = new Fighter(Holding(QiStrike), 100, 0, 0, null, maxQi: 100, qi: 29);
        Assert.False(f.CanAfford(QiStrike));
        f.Tick(Square);
        Assert.True(f.CanAct);
        Assert.Equal(29, f.Qi);
    }

    [Fact]
    public void A_qi_technique_pays_its_cost_when_it_starts()
    {
        var f = new Fighter(Holding(QiStrike), 100, 0, 0, null, maxQi: 100, qi: 40);
        f.Tick(Square);
        Assert.Same(QiStrike, f.Technique);
        Assert.Equal(10, f.Qi);
    }

    [Fact]
    public void Earned_qi_can_pay_for_a_qi_technique()
    {
        // One starts on 28 Qi: one landed hit (+2) is exactly enough for the 30-Qi strike.
        var d = Run(Punch, Punch, twoPressTick: -1, oneQi: 28);
        Assert.Equal(30, d.One.Qi);
        Assert.True(d.One.CanAfford(QiStrike));
    }

    [Fact]
    public void A_fighter_without_qi_gains_none()
    {
        var f = new Fighter(Holding(Punch));
        Assert.Equal(0, f.GainQi(10));
        Assert.Equal(0, f.Qi);
    }

    // ---------- The real sheet ----------

    [Fact]
    public void The_real_sheet_gives_the_briefs_qi_numbers()
    {
        var r = RulesCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("rules.csv"))).Rules!;
        Assert.Equal(100, r.QiMax);
        Assert.Equal(0, r.QiStart);
        Assert.Equal(2, r.QiPerHit);
        Assert.Equal(10, r.ParryPerfectQi);
        Assert.Equal(10, r.ClashPerfectQi);
    }
}
