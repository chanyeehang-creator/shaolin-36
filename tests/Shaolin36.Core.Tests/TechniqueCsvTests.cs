using Shaolin36.Core.Data;
using Xunit;

namespace Shaolin36.Core.Tests;

public class TechniqueCsvTests
{
    private const string Header = "id,name,category,weight,weapon,startup,active,recovery,clash_window,perfect_clash,stamina,qi,damage,clashable";

    [Fact]
    public void Reads_a_valid_row()
    {
        var r = TechniqueCsv.Parse(Header + "\nluohan,罗汉拳,waigong,medium,false,10,3,14,4,2,8,0,6,true\n");
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
        var r = TechniqueCsv.Parse(Header + "\ntiesha,铁砂掌,内功,重,false,18,3,20,5,2,15,0,14,true");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Equal(Category.Neigong, r.Techniques[0].Category);
        Assert.Equal(Weight.Heavy, r.Techniques[0].Weight);
    }

    [Fact]
    public void Skips_comments_blank_lines_and_excel_bom()
    {
        var r = TechniqueCsv.Parse("﻿# comment\n\n" + Header + "\r\n# another\r\na,A,waigong,light,false,5,2,8,2,1,3,0,2,true\r\n\r\n");
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.Single(r.Techniques);
    }

    [Fact]
    public void Quoted_names_may_contain_commas()
    {
        var r = TechniqueCsv.Parse(Header + "\na,\"Fist, Arhat\",waigong,light,false,5,2,8,2,1,3,0,2,true");
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
    [InlineData("a,A,kungfu,light,false,5,2,8,2,1,3,0,2,true", "unknown category")]
    [InlineData("a,A,waigong,huge,false,5,2,8,2,1,3,0,2,true", "unknown weight")]
    [InlineData("a,A,waigong,light,maybe,5,2,8,2,1,3,0,2,true", "'weapon' must be true or false")]
    [InlineData("a,A,waigong,light,false,x,2,8,2,1,3,0,2,true", "'startup' must be a whole number")]
    [InlineData("a,A,waigong,light,false,0,2,8,0,0,3,0,2,true", "'startup' must be at least 1")]
    [InlineData("a,A,waigong,light,false,5,2,8,6,1,3,0,2,true", "cannot be longer than 'startup'")]
    [InlineData("a,A,waigong,light,false,5,2,8,2,3,3,0,2,true", "cannot be longer than 'clash_window'")]
    [InlineData("a,A,qinna,light,false,5,2,8,2,1,3,0,2,false", "unclashable technique must have 'clash_window' 0")]
    [InlineData(",A,waigong,light,false,5,2,8,2,1,3,0,2,true", "'id' is empty")]
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
        var row = "a,A,waigong,light,false,5,2,8,2,1,3,0,2,true";
        var r = TechniqueCsv.Parse(Header + "\n" + row + "\n" + row);
        Assert.Contains(r.Errors, e => e.StartsWith("line 3") && e.Contains("duplicate id 'a'"));
    }
}
