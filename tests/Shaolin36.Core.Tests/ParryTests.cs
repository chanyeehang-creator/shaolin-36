using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

public class ParryTests
{
    private static readonly Rules R = Make.TestRules();
    private static readonly Technique P = Make.ParryMove(stamina: 10); // window frames 3-12, perfect 3-6

    [Theory]
    [InlineData(0, ParryOutcome.NotParried)]  // still starting up
    [InlineData(2, ParryOutcome.NotParried)]
    [InlineData(3, ParryOutcome.PerfectParry)]
    [InlineData(6, ParryOutcome.PerfectParry)]
    [InlineData(7, ParryOutcome.Parried)]
    [InlineData(12, ParryOutcome.Parried)]
    [InlineData(13, ParryOutcome.NotParried)] // recovering from a missed parry
    public void Outcome_depends_on_the_parry_frame(int frame, ParryOutcome expected)
    {
        Assert.Equal(expected, Parry.Resolve(P, frame));
    }

    [Theory]
    [InlineData(1, ParryOutcome.NotParried)]   // still starting up (2 frames)
    [InlineData(2, ParryOutcome.Parried)]      // early block: pressed a bit early, still blocked
    [InlineData(4, ParryOutcome.Parried)]
    [InlineData(5, ParryOutcome.PerfectParry)] // then the 4 perfect frames
    [InlineData(8, ParryOutcome.PerfectParry)]
    [InlineData(9, ParryOutcome.Parried)]      // then the late block
    [InlineData(18, ParryOutcome.Parried)]
    [InlineData(19, ParryOutcome.NotParried)]  // window closed
    public void An_early_block_window_comes_before_the_perfect_frames(int frame, ParryOutcome expected)
    {
        var p = new Technique("p", "p", Category.Huajin, Weight.Light, false, 2, 17, 10, 0, 0, 10, 0, 0, false,
            perfectParryFrames: 4, earlyBlockFrames: 3);
        Assert.Equal(5, p.PerfectParryStart);
        Assert.Equal(expected, Parry.Resolve(p, frame));
    }

    [Fact]
    public void An_attack_never_parries()
    {
        Assert.Equal(ParryOutcome.NotParried, Parry.Resolve(Make.Move(), 10));
    }

    [Fact]
    public void Plain_parry_deflects_without_punishing_the_attacker()
    {
        var e = Parry.Effect(ParryOutcome.Parried, P, R);
        Assert.True(e.Deflected);
        Assert.Equal(R.ParryRecovery, e.DefenderRecoveryFrames);
        Assert.Equal(0, e.DefenderStaminaRefund);
        Assert.Equal(0, e.DefenderQiGain);
        Assert.Equal(0, e.AttackerStaggerFrames);
        Assert.Equal(0, e.AttackerStaminaLoss);
    }

    [Fact]
    public void Perfect_parry_refunds_the_parry_cost_and_staggers_and_drains_the_attacker()
    {
        var e = Parry.Effect(ParryOutcome.PerfectParry, P, R);
        Assert.True(e.Deflected);
        Assert.Equal(R.ParryPerfectRecovery, e.DefenderRecoveryFrames);
        Assert.Equal(10, e.DefenderStaminaRefund);
        Assert.Equal(R.ParryPerfectQi, e.DefenderQiGain);
        Assert.Equal(R.ParryPerfectStagger, e.AttackerStaggerFrames);
        Assert.Equal(R.ParryPerfectDrain, e.AttackerStaminaLoss);
    }

    [Fact]
    public void Missed_parry_changes_nothing()
    {
        Assert.False(Parry.Effect(ParryOutcome.NotParried, P, R).Deflected);
    }
}
