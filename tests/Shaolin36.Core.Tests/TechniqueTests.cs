using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

public class TechniqueTests
{
    [Fact]
    public void Total_frames_is_startup_plus_active_plus_recovery()
    {
        Assert.Equal(25, Make.Move(startup: 10, active: 3, recovery: 12).TotalFrames);
    }

    [Fact]
    public void Clash_window_is_the_last_frames_of_startup()
    {
        var m = Make.Move(startup: 10, window: 4, perfect: 2);
        // startup is frames 0-9, so the window is frames 6-9
        Assert.False(m.IsInClashWindow(5));
        Assert.True(m.IsInClashWindow(6));
        Assert.True(m.IsInClashWindow(9));
        Assert.False(m.IsInClashWindow(10)); // active now
    }

    [Fact]
    public void Perfect_clash_is_the_first_frames_of_the_window()
    {
        var m = Make.Move(startup: 10, window: 4, perfect: 2);
        Assert.True(m.IsInPerfectClash(6));
        Assert.True(m.IsInPerfectClash(7));
        Assert.False(m.IsInPerfectClash(8));
    }

    [Fact]
    public void Active_frames_follow_startup()
    {
        var m = Make.Move(startup: 10, active: 3);
        Assert.False(m.IsActive(9));
        Assert.True(m.IsActive(10));
        Assert.True(m.IsActive(12));
        Assert.False(m.IsActive(13));
    }

    [Fact]
    public void Sixty_frames_is_one_second()
    {
        Assert.Equal(1.0, Sim.FramesToSeconds(60));
    }

    [Fact]
    public void Link_frames_count_chain_frames_that_land_before_hitstun_ends()
    {
        // 18 hitstun - 3 active - 9 next startup = 6: chaining on recovery frames 0-5 links.
        var first = Make.Move(active: 3, recovery: 16, hitstun: 18, next: "b", chain: 8);
        var next = Make.Move("b", startup: 9);
        Assert.Equal(6, first.LinkFramesInto(next));
    }

    [Fact]
    public void Link_frames_are_capped_by_the_chain_window()
    {
        var first = Make.Move(active: 3, recovery: 16, hitstun: 40, next: "b", chain: 8);
        Assert.Equal(8, first.LinkFramesInto(Make.Move("b", startup: 9)));
    }

    [Fact]
    public void A_string_with_too_little_hitstun_never_links()
    {
        var first = Make.Move(active: 3, recovery: 16, hitstun: 10, next: "b", chain: 8);
        Assert.Equal(0, first.LinkFramesInto(Make.Move("b", startup: 9)));
    }

    [Fact]
    public void Parry_window_is_the_active_frames_and_perfect_is_the_first_of_them()
    {
        var p = Make.ParryMove(); // startup 3, active frames 3-12, perfect 3-6
        Assert.False(p.IsParrying(2));
        Assert.True(p.IsInPerfectParry(3));
        Assert.True(p.IsInPerfectParry(6));
        Assert.False(p.IsInPerfectParry(7));
        Assert.True(p.IsParrying(7));
        Assert.True(p.IsParrying(12));
        Assert.False(p.IsParrying(13)); // recovery
    }

    [Fact]
    public void An_attack_is_never_a_parry()
    {
        Assert.False(Make.Move().IsParrying(10));
    }
}
