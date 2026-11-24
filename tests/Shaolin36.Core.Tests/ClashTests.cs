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
}
