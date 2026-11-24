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
}
