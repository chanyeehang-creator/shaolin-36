using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>One test per row of the clash table in the concept doc.</summary>
public class ClashTests
{
    // Target winds up a 10-frame startup with a 4-frame window (frames 6-9), perfect on frames 6-7.
    private const int OutsideWindow = 3;
    private const int InWindow = 8;
    private const int InPerfect = 6;

    [Fact]
    public void Hit_outside_the_window_is_not_a_clash()
    {
        var outcome = Clash.Resolve(Make.Move(), Make.Move(), OutsideWindow);
        Assert.Equal(ClashOutcome.NoClash, outcome);
    }

    [Fact]
    public void Same_weight_both_recoil()
    {
        var outcome = Clash.Resolve(Make.Move(weight: Weight.Medium), Make.Move(weight: Weight.Medium), InWindow);
        Assert.Equal(ClashOutcome.BothRecoil, outcome);
    }

    [Theory]
    [InlineData(Weight.Heavy, Weight.Medium)]
    [InlineData(Weight.Medium, Weight.Light)]
    [InlineData(Weight.Heavy, Weight.Light)]
    public void Heavier_striker_wins(Weight striker, Weight target)
    {
        var outcome = Clash.Resolve(Make.Move(weight: striker), Make.Move(weight: target), InWindow);
        Assert.Equal(ClashOutcome.StrikerWins, outcome);
    }

    [Fact]
    public void Lighter_striker_is_interrupted()
    {
        var outcome = Clash.Resolve(Make.Move(weight: Weight.Light), Make.Move(weight: Weight.Heavy), InWindow);
        Assert.Equal(ClashOutcome.TargetWins, outcome);
    }

    [Fact]
    public void Perfect_clash_breaks_the_target_whatever_the_weights()
    {
        var outcome = Clash.Resolve(Make.Move(weight: Weight.Light), Make.Move(weight: Weight.Heavy), InPerfect);
        Assert.Equal(ClashOutcome.PerfectClash, outcome);
    }

    [Fact]
    public void Unclashable_striker_never_clashes()
    {
        var grab = Make.Move(window: 0, perfect: 0, clashable: false, category: Category.Qinna);
        Assert.Equal(ClashOutcome.NoClash, Clash.Resolve(grab, Make.Move(), InWindow));
    }

    [Fact]
    public void Unclashable_target_never_clashes()
    {
        var grab = Make.Move(window: 0, perfect: 0, clashable: false, category: Category.Qinna);
        Assert.Equal(ClashOutcome.NoClash, Clash.Resolve(Make.Move(), grab, InWindow));
    }

    [Fact]
    public void Weapon_counts_one_step_heavier_against_bare_hands()
    {
        var staff = Make.Move(weight: Weight.Medium, weapon: true);
        var fist = Make.Move(weight: Weight.Medium);
        Assert.Equal(ClashOutcome.StrikerWins, Clash.Resolve(staff, fist, InWindow));
        Assert.Equal(ClashOutcome.TargetWins, Clash.Resolve(fist, staff, InWindow));
    }

    [Fact]
    public void Weapon_against_weapon_gets_no_bonus()
    {
        var staff = Make.Move(weight: Weight.Medium, weapon: true);
        Assert.Equal(ClashOutcome.BothRecoil, Clash.Resolve(staff, staff, InWindow));
    }

    // Effects: each result takes its numbers from the matching rules key.
    private static readonly Rules R = Make.TestRules();

    [Fact]
    public void Same_weight_effect_recoils_both_and_costs_both_stamina()
    {
        var e = Clash.Effect(ClashOutcome.BothRecoil, R);
        Assert.False(e.StrikerHitLands);
        foreach (var side in new[] { e.Striker, e.Target })
        {
            Assert.True(side.MoveCancelled);
            Assert.Equal(R.ClashRecoilFrames, side.StunFrames);
            Assert.Equal(R.ClashRecoilStamina, side.StaminaLoss);
            Assert.Equal(0, side.QiGain);
        }
    }

    [Fact]
    public void Heavier_striker_effect_lands_the_hit_and_staggers_the_target()
    {
        var e = Clash.Effect(ClashOutcome.StrikerWins, R);
        Assert.True(e.StrikerHitLands);
        Assert.False(e.Striker.MoveCancelled);
        Assert.True(e.Target.MoveCancelled);
        Assert.Equal(R.ClashLoseStagger, e.Target.StunFrames);
        Assert.Equal(R.ClashLoseStamina, e.Target.StaminaLoss);
    }

    [Fact]
    public void Lighter_striker_effect_cancels_the_striker_and_the_target_carries_on()
    {
        var e = Clash.Effect(ClashOutcome.TargetWins, R);
        Assert.False(e.StrikerHitLands);
        Assert.True(e.Striker.MoveCancelled);
        Assert.Equal(R.ClashLoseStagger, e.Striker.StunFrames);
        Assert.Equal(R.ClashLoseStamina, e.Striker.StaminaLoss);
        Assert.False(e.Target.MoveCancelled);
    }

    [Fact]
    public void Perfect_clash_effect_breaks_the_target_and_gives_the_striker_qi()
    {
        var e = Clash.Effect(ClashOutcome.PerfectClash, R);
        Assert.False(e.StrikerHitLands);
        Assert.True(e.Striker.MoveCancelled);
        Assert.Equal(R.ClashPerfectRecovery, e.Striker.StunFrames);
        Assert.Equal(R.ClashPerfectQi, e.Striker.QiGain);
        Assert.True(e.Target.MoveCancelled);
        Assert.Equal(R.ClashPerfectStagger, e.Target.StunFrames);
    }

    [Fact]
    public void No_clash_effect_changes_nothing()
    {
        var e = Clash.Effect(ClashOutcome.NoClash, R);
        Assert.True(e.StrikerHitLands);
        Assert.False(e.Striker.MoveCancelled);
        Assert.False(e.Target.MoveCancelled);
    }
}
