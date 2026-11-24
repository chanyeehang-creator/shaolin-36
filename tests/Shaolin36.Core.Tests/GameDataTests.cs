using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The real sheets shipped with the game must always pass the check.</summary>
public class GameDataTests
{
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
}
