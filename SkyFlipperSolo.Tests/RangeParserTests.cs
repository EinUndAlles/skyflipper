using NUnit.Framework;
using SkyFlipperSolo.Services.Filters;

namespace SkyFlipperSolo.Tests;

[TestFixture]
public class RangeParserTests
{
    [Test]
    public void ParseLongRanges_Empty_ReturnsEmpty()
    {
        Assert.That(RangeParser.ParseLongRanges(null), Is.Empty);
        Assert.That(RangeParser.ParseLongRanges(""), Is.Empty);
        Assert.That(RangeParser.ParseLongRanges("  "), Is.Empty);
    }

    [Test]
    public void ParseLongRanges_ExactValue()
    {
        var result = RangeParser.ParseLongRanges("5");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(5));
        Assert.That(result[0].Max, Is.EqualTo(5));
    }

    [Test]
    public void ParseLongRanges_Range()
    {
        var result = RangeParser.ParseLongRanges("3-7");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(3));
        Assert.That(result[0].Max, Is.EqualTo(7));
    }

    [Test]
    public void ParseLongRanges_ReversedRange_Normalizes()
    {
        var result = RangeParser.ParseLongRanges("7-3");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(3));
        Assert.That(result[0].Max, Is.EqualTo(7));
    }

    [Test]
    public void ParseLongRanges_GreaterThan()
    {
        var result = RangeParser.ParseLongRanges(">5");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(6));
        Assert.That(result[0].Max, Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void ParseLongRanges_LessThan()
    {
        var result = RangeParser.ParseLongRanges("<10");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(1));
        Assert.That(result[0].Max, Is.EqualTo(9));
    }

    [Test]
    public void ParseLongRanges_LessThanOne_ClampsToOne()
    {
        var result = RangeParser.ParseLongRanges("<0");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(1));
        Assert.That(result[0].Max, Is.EqualTo(1));
    }

    [Test]
    public void ParseLongRanges_Any_MapsToGreaterThanZero()
    {
        var result = RangeParser.ParseLongRanges("any");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(1));
        Assert.That(result[0].Max, Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void ParseLongRanges_None_MapsToZero()
    {
        var result = RangeParser.ParseLongRanges("none");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(0));
        Assert.That(result[0].Max, Is.EqualTo(0));
    }

    [Test]
    public void ParseLongRanges_TrailingDash_TreatedAsGreater()
    {
        var result = RangeParser.ParseLongRanges("5-");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(6));
        Assert.That(result[0].Max, Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void ParseLongRanges_PipeSeparated_MultipleRanges()
    {
        var result = RangeParser.ParseLongRanges("1-3|5-7");
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Min, Is.EqualTo(1));
        Assert.That(result[0].Max, Is.EqualTo(3));
        Assert.That(result[1].Min, Is.EqualTo(5));
        Assert.That(result[1].Max, Is.EqualTo(7));
    }

    [Test]
    public void ParseLongRanges_UnderscoreInNumber_Ignored()
    {
        var result = RangeParser.ParseLongRanges("1_000_000");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(1000000));
        Assert.That(result[0].Max, Is.EqualTo(1000000));
    }

    [Test]
    public void ParseLongRanges_InvalidInput_ReturnsEmpty()
    {
        Assert.That(RangeParser.ParseLongRanges("abc"), Is.Empty);
        Assert.That(RangeParser.ParseLongRanges("a-b"), Is.Empty);
    }

    [TestCase("0", 0, 0)]
    [TestCase("100", 100, 100)]
    [TestCase("1-5", 1, 5)]
    [TestCase(">10", 11, long.MaxValue)]
    [TestCase("<5", 1, 4)]
    [TestCase("any", 1, long.MaxValue)]
    [TestCase("none", 0, 0)]
    public void ParseLongRanges_CommonPatterns(string input, long expectedMin, long expectedMax)
    {
        var result = RangeParser.ParseLongRanges(input);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Min, Is.EqualTo(expectedMin));
        Assert.That(result[0].Max, Is.EqualTo(expectedMax));
    }
}
