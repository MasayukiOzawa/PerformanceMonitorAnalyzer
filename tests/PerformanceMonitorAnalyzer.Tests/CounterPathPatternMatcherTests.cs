using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterPathPatternMatcherTests
{
    [Theory]
    [InlineData(
        @"\SQLServer:Batch_Resp_Statistics(Elapsed_Time:Total(ms))\*",
        @"\SQLServer:Batch_Resp_Statistics(Elapsed_Time:Total(ms))\Batch Requests/sec")]
    [InlineData(
        @"\SQLServer:Batch_Resp_Statistics(Elapsed_Time:Total(ms))\*",
        @"\\MACHINE01\SQLServer:Batch_Resp_Statistics(Elapsed_Time:Total(ms))\Average wait time (ms)")]
    public void MatchesPattern_ObjectWildcard_MatchesAllCountersUnderSameObject(string pattern, string counterPath)
    {
        Assert.True(CounterPathPatternMatcher.MatchesPattern(pattern, counterPath));
    }

    [Fact]
    public void MatchesPattern_ObjectWildcard_DoesNotMatchDifferentObject()
    {
        var pattern = @"\SQLServer:Batch_Resp_Statistics(Elapsed_Time:Total(ms))\*";
        var counterPath = @"\SQLServer:Locks(_Total)\Lock Waits/sec";

        Assert.False(CounterPathPatternMatcher.MatchesPattern(pattern, counterPath));
    }

    [Fact]
    public void MatchesPattern_QuestionAndAsteriskWildcards_StillMatchInstancePatterns()
    {
        var pattern = @"\Network Interface(*)\*";
        var counterPath = @"\\MACHINE01\Network Interface(Intel[R] Ethernet Connection)\Bytes Total/sec";

        Assert.True(CounterPathPatternMatcher.MatchesPattern(pattern, counterPath));
    }
}
