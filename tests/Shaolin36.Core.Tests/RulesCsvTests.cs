using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

public class RulesCsvTests
{
    [Fact]
    public void Reads_every_key()
    {
        var r = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6")));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(Make.RuleValue("hp_max"), r.Rules!.HpMax);
        Assert.Equal(Make.RuleValue("clash_perfect_qi"), r.Rules.ClashPerfectQi);
        Assert.Equal(Make.RuleValue("parry_perfect_qi"), r.Rules.ParryPerfectQi);
        Assert.Equal(50, r.Rules.ExhaustedUntil);
        Assert.Equal(6, r.Rules.InputBuffer);
    }

    [Fact]
    public void Every_key_has_its_own_property()
    {
        // Distinct values per key: if two properties read the same key, two values here collide.
        // (clash_recoil_push is 0 by default in tests, like qi_start, so give it its own value here.)
        var rules = RulesCsv.Parse(Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6"),
            ("clash_recoil_push", Make.RuleValue("clash_recoil_push").ToString()))).Rules!;
        var values = typeof(Rules).GetProperties()
            .Where(p => p.PropertyType == typeof(int))
            .Select(p => (int)p.GetValue(rules)!)
            .ToList();
        Assert.Equal(Rules.Keys.Length, values.Count);
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void Reports_a_missing_key()
    {
        var text = string.Join("\n", Make.RulesText().Split('\n').Where(l => !l.StartsWith("qi_per_hit,")));
        var r = RulesCsv.Parse(text);
        Assert.False(r.Ok);
        Assert.Null(r.Rules);
        Assert.Contains(r.Errors, e => e.Contains("missing key 'qi_per_hit'"));
    }

    [Theory]
    [InlineData("hp_maxx,100,typo", "unknown key 'hp_maxx'")]
    [InlineData("hp_max,100,again", "duplicate key 'hp_max'")]
    public void Reports_bad_keys_with_a_line_number(string extra, string expected)
    {
        var lines = Make.RulesText(("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6")).TrimEnd('\n') + "\n" + extra;
        var r = RulesCsv.Parse(lines);
        int lineNo = Rules.Keys.Length + 2;
        Assert.Contains(r.Errors, e => e.StartsWith($"line {lineNo}") && e.Contains(expected));
    }

    [Theory]
    [InlineData("stamina_regen", "fast", "whole number 0 or more")]
    [InlineData("stamina_regen", "-1", "whole number 0 or more")]
    [InlineData("hp_max", "0", "must be at least 1")]
    [InlineData("exhausted_until", "500", "cannot be more than 'stamina_max'")]
    [InlineData("qi_start", "500", "cannot be more than 'qi_max'")]
    [InlineData("exhausted_damage_pct", "80", "must be 100 or more")]
    [InlineData("input_buffer", "31", "must be 30 frames or fewer")]
    [InlineData("walk_speed", "2001", "2000 cm per second or less")]
    [InlineData("min_distance", "0", "must be at least 1")]
    [InlineData("start_distance", "10", "cannot be less than 'min_distance'")]
    [InlineData("start_distance", "100000", "cannot be more than 'arena_width'")]
    public void Rejects_bad_values(string key, string value, string expected)
    {
        var overrides = new List<(string, string)> { ("exhausted_until", "50"), ("qi_start", "0"), ("input_buffer", "6") };
        overrides.Add((key, value)); // later override wins
        var r = RulesCsv.Parse(Make.RulesText(overrides.ToArray()));
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains($"({key})") && e.Contains(expected));
    }

    [Fact]
    public void Rejects_a_bad_header()
    {
        var r = RulesCsv.Parse("name,amount\nhp_max,100");
        Assert.Contains(r.Errors, e => e.StartsWith("line 1") && e.Contains("'key,value'"));
    }
}
