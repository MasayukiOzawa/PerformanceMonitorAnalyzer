using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CsvLineParserTests
{
    [Fact]
    public void Parse_HandlesCommaInsideQuotes()
    {
        Assert.Equal(new[] { "a,b", "c" }, CsvLineParser.Parse("\"a,b\",c"));
    }

    [Fact]
    public void Parse_HandlesEscapedQuotes()
    {
        Assert.Equal(new[] { "a\"b", "c" }, CsvLineParser.Parse("\"a\"\"b\",c"));
    }

    [Fact]
    public void Parse_PreservesEmptyFields()
    {
        Assert.Equal(new[] { "", "b", "" }, CsvLineParser.Parse(",b,"));
    }

    [Fact]
    public void TryParseTimestamp_ReturnsParsedFirstColumn()
    {
        Assert.True(CsvLineParser.TryParseTimestamp("\"2026-01-02 03:04:05\",1", out var timestamp));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), timestamp);
    }
}
