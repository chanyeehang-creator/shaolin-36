using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The real sheets shipped with the game must always pass the check.</summary>
public class GameDataTests
{
    /// <summary>Path to a sheet in game/data.</summary>
    internal static string SheetPath(string file) => Path.Combine(RepoRoot(), "game", "data", file);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Shaolin36.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root (Shaolin36.sln)");
    }

    [Fact]
    public void Technique_sheet_passes_the_check()
    {
        var path = Path.Combine(RepoRoot(), "game", "data", "techniques.csv");
        var r = TechniqueCsv.Parse(File.ReadAllText(path));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.NotEmpty(r.Techniques);
    }

    [Fact]
    public void Rules_sheet_passes_the_check()
    {
        var path = Path.Combine(RepoRoot(), "game", "data", "rules.csv");
        var r = RulesCsv.Parse(File.ReadAllText(path));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
    }

    [Fact]
    public void Luohan_quan_is_a_four_hit_string()
    {
        var path = Path.Combine(RepoRoot(), "game", "data", "techniques.csv");
        var byId = TechniqueCsv.Parse(File.ReadAllText(path)).Techniques.ToDictionary(t => t.Id);
        var hits = new List<string>();
        for (var t = byId["luohan_quan_1"]; ; t = byId[t.Next])
        {
            hits.Add(t.Id);
            if (!t.HasNext || hits.Count > 10) break;
        }
        Assert.Equal(new[] { "luohan_quan_1", "luohan_quan_2", "luohan_quan_3", "luohan_quan_4" }, hits);
    }

    [Fact]
    public void Timed_parry_has_a_perfect_window()
    {
        var path = Path.Combine(RepoRoot(), "game", "data", "techniques.csv");
        var parry = TechniqueCsv.Parse(File.ReadAllText(path)).Techniques.Single(t => t.Id == "timed_parry");
        Assert.Equal(Category.Huajin, parry.Category);
        Assert.True(parry.PerfectParryFrames > 0);
    }

    [Fact]
    public void Timed_parry_has_block_frames_either_side_of_the_perfect_ones_and_a_cooldown()
    {
        // Chan's design: a short block window (leeway for pressing early), then the perfect frames, then a longer
        // block window, then a cooldown so it can't be held.
        var path = Path.Combine(RepoRoot(), "game", "data", "techniques.csv");
        var parry = TechniqueCsv.Parse(File.ReadAllText(path)).Techniques.Single(t => t.Id == "timed_parry");
        Assert.True(parry.EarlyBlockFrames > 0, "the window needs block frames before the perfect ones");
        Assert.True(parry.ActiveFrames > parry.EarlyBlockFrames + parry.PerfectParryFrames, "the window needs block frames after the perfect ones");
        Assert.True(parry.RecoveryFrames > 0, "the parry needs a cooldown");
        Assert.Equal(Combat.ParryOutcome.Parried, Combat.Parry.Resolve(parry, parry.StartupFrames));
        Assert.Equal(Combat.ParryOutcome.PerfectParry, Combat.Parry.Resolve(parry, parry.PerfectParryStart));
        Assert.Equal(Combat.ParryOutcome.Parried, Combat.Parry.Resolve(parry, parry.PerfectParryStart + parry.PerfectParryFrames));
    }

    [Fact]
    public void Timed_parry_opens_in_2_frames_and_keeps_its_4_perfect_frames()
    {
        // Chan's balance patch: 2-frame wind-up, a 3-frame block before the perfect parry, the 4 perfect frames as they were.
        var path = Path.Combine(RepoRoot(), "game", "data", "techniques.csv");
        var parry = TechniqueCsv.Parse(File.ReadAllText(path)).Techniques.Single(t => t.Id == "timed_parry");
        Assert.Equal(2, parry.StartupFrames);
        Assert.Equal(3, parry.EarlyBlockFrames);
        Assert.Equal(4, parry.PerfectParryFrames);
        var rules = RulesCsv.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "game", "data", "rules.csv"))).Rules!;
        Assert.Equal(5, rules.ParryRecovery);
    }
}
