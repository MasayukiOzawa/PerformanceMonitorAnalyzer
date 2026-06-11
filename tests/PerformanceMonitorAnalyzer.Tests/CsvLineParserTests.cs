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
}
