using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

public class TechniqueCsvTests
{
    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable,hitstun,reach,next,chain_window,perfect_parry";

    [Fact]
    public void Reads_a_valid_row()
    {
        var r = TechniqueCsv.Parse(Header + "\nluohan,罗汉拳,waigong,medium,false,10,3,14,4,2,8,0,6,true,18,120,,0,0\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var t = Assert.Single(r.Techniques);
        Assert.Equal("罗汉拳", t.Name);
        Assert.Equal(Category.Waigong, t.Category);
        Assert.Equal(Weight.Medium, t.Weight);
        Assert.Equal(27, t.TotalFrames);
    }

    [Fact]
    public void Accepts_chinese_category_and_weight_names()
    {
        var r = TechniqueCsv.Parse(Header + "\ntiesha,铁砂掌,内功,重,false,18,3,20,5,2,15,0,14,true,12,100,,0,0");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(Category.Neigong, r.Techniques[0].Category);
        Assert.Equal(Weight.Heavy, r.Techniques[0].Weight);
    }

    [Fact]
    public void Skips_comments_blank_lines_and_excel_bom()
    {
        var r = TechniqueCsv.Parse("﻿# comment\n\n" + Header + "\r\n# another\r\na,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0\r\n\r\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Single(r.Techniques);
    }

    [Fact]
    public void Quoted_names_may_contain_commas()
    {
        var r = TechniqueCsv.Parse(Header + "\na,\"Fist, Arhat\",waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal("Fist, Arhat", r.Techniques[0].Name);
    }

    [Fact]
    public void Reports_missing_column()
    {
        var r = TechniqueCsv.Parse("id,name\na,A");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Contains("missing column 'category'"));
    }

    [Theory]
    [InlineData("a,A,kungfu,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0", "unknown category")]
    [InlineData("a,A,waigong,huge,false,5,2,8,2,1,3,0,2,true,12,100,,0,0", "unknown weight")]
    [InlineData("a,A,waigong,light,maybe,5,2,8,2,1,3,0,2,true,12,100,,0,0", "'weapon' must be true or false")]
    [InlineData("a,A,waigong,light,false,x,2,8,2,1,3,0,2,true,12,100,,0,0", "'startup' must be a whole number")]
    [InlineData("a,A,waigong,light,false,0,2,8,0,0,3,0,2,true,12,100,,0,0", "'startup' must be at least 1")]
    [InlineData("a,A,waigong,light,false,5,2,8,6,1,3,0,2,true,12,100,,0,0", "cannot be longer than 'startup'")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,3,3,0,2,true,12,100,,0,0", "cannot be longer than 'clash_window'")]
    [InlineData("a,A,qinna,light,false,5,2,8,2,1,3,0,2,false,12,100,,0,0", "unclashable technique must have 'clash_window' 0")]
    [InlineData(",A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0", "'id' is empty")]
    public void Rejects_bad_rows_with_a_line_number(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + "\n" + row);
        Assert.False(r.Ok);
        Assert.Empty(r.Techniques);
        Assert.Contains(r.Errors, e => e.StartsWith("line 2") && e.Contains(expected));
    }

    [Fact]
    public void Rejects_duplicate_ids()
    {
        var row = "a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0";
        var r = TechniqueCsv.Parse(Header + "\n" + row + "\n" + row);
        Assert.Contains(r.Errors, e => e.StartsWith("line 3") && e.Contains("duplicate id 'a'"));
    }

    [Fact]
    public void Reads_the_string_and_parry_columns()
    {
        var r = TechniqueCsv.Parse(Header +
            "\na,A,waigong,medium,false,11,3,16,5,2,8,0,6,true,18,120,b,8,0" +
            "\nb,B,waigong,medium,false,9,3,16,4,2,8,0,6,true,18,120,,0,0" +
            "\np,P,huajin,light,false,3,10,20,0,0,10,0,0,false,0,0,,0,4");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var a = r.Techniques[0];
        Assert.Equal(18, a.HitstunFrames);
        Assert.Equal(120, a.ReachCm);
        Assert.Equal("b", a.Next);
        Assert.Equal(8, a.ChainWindowFrames);
        Assert.False(r.Techniques[1].HasNext);
        Assert.Equal(4, r.Techniques[2].PerfectParryFrames);
    }

    [Theory]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,b,9,0", "'chain_window' (9) cannot be longer than 'recovery' (8)")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,b,0,0", "'chain_window' is 0")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,4,0", "'next' is empty")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,a,4,0", "cannot be the technique itself")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,1", "only 化劲 (huajin) techniques")]
    [InlineData("a,A,huajin,light,false,3,4,8,0,0,3,0,0,false,0,0,,0,5", "'perfect_parry' (5) cannot be longer than 'active' (4)")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,1,3,0,2,true,x,100,,0,0", "'hitstun' must be a whole number")]
    public void Rejects_bad_string_and_parry_values(string row, string expected)
    {
        var r = TechniqueCsv.Parse(Header + "\n" + row);
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.StartsWith("line 2") && e.Contains(expected));
    }

    [Fact]
    public void Rejects_next_that_points_at_no_technique()
    {
        var r = TechniqueCsv.Parse(Header + "\na,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,missing,4,0");
        Assert.Contains(r.Errors, e => e.StartsWith("line 2") && e.Contains("'next' is 'missing', but no technique has that id"));
    }

    [Fact]
    public void Next_may_point_at_a_later_row()
    {
        var r = TechniqueCsv.Parse(Header +
            "\na,A,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,b,4,0" +
            "\nb,B,waigong,light,false,5,2,8,2,1,3,0,2,true,12,100,,0,0");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
    }
}
