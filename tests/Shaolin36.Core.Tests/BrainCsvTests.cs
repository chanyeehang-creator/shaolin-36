using Shaolin36.Core.Brains;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>The brain sheet check (game/data/brains.csv).</summary>
public class BrainCsvTests
{
    private const string Header = "id,name,reaction,think_every,aggression,punish,chain,parry,perfect,step,circle,rest_below,heavy,guess,note\n";

    [Fact]
    public void The_real_sheet_passes_and_has_the_three_brains()
    {
        var r = BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(new[] { "novice", "monk", "master" }, r.Sheet!.Styles.Select(s => s.Id));
        var monk = r.Sheet.Find("monk")!;
        Assert.InRange(monk.Reaction, 15, 20); // reacts like a person
    }

    [Fact]
    public void A_good_row_loads_every_column()
    {
        var r = BrainCsv.Parse(Header + "x,X brain,18,20,50,60,80,40,35,20,50,25,25,15,note\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var s = r.Sheet!.Styles.Single();
        Assert.Equal(("x", "X brain", 18, 20, 50, 60, 80), (s.Id, s.Name, s.Reaction, s.ThinkEvery, s.Aggression, s.Punish, s.Chain));
        Assert.Equal((40, 35, 20, 50, 25, 25, 15), (s.Parry, s.Perfect, s.Step, s.Circle, s.RestBelow, s.Heavy, s.Guess));
    }

    [Theory]
    [InlineData("x,X,61,20,50,60,80,40,35,20,50,25,25,15", "reaction")]
    [InlineData("x,X,18,0,50,60,80,40,35,20,50,25,25,15", "think_every")]
    [InlineData("x,X,18,20,101,60,80,40,35,20,50,25,25,15", "aggression")]
    [InlineData("x,X,18,20,50,60,80,-1,35,20,50,25,25,15", "parry")]
    [InlineData("x,X,18,20,50,60,80,40,35,20,50,25,lots,15", "heavy")]
    [InlineData("x,X,18,20,50,60,80,40,35,20,50,25,25,200", "guess")]
    public void Bad_numbers_are_reported_with_line_and_column(string row, string column)
    {
        var r = BrainCsv.Parse(Header + row + "\n");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.StartsWith("line 2 (x, " + column + ")"));
    }

    [Fact]
    public void Duplicate_ids_missing_columns_and_empty_sheets_are_reported()
    {
        var dup = BrainCsv.Parse(Header + "x,X,18,20,50,60,80,40,35,20,50,25,25,15\nx,X,18,20,50,60,80,40,35,20,50,25,25,15\n");
        Assert.Contains(dup.Errors, e => e.Contains("duplicate id 'x'"));

        var missing = BrainCsv.Parse("id,name,reaction\nx,X,18\n");
        Assert.Contains(missing.Errors, e => e.Contains("missing column 'think_every'"));

        Assert.False(BrainCsv.Parse(Header).Ok);
        Assert.False(BrainCsv.Parse("").Ok);
    }
}
