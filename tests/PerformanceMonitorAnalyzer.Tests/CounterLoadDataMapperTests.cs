namespace PerformanceMonitorAnalyzer.Tests;

public class CounterLoadDataMapperTests
{
    [Fact]
    public void Map_ConvertsValidPointsAndSkipsNaN()
    {
        var counterInfo = new BlgFileAnalyzer.CounterInfo
        {
            DataPoints =
            [
                new BlgFileAnalyzer.CounterDataPoint
                {
                    Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Value = 10.5
                },
                new BlgFileAnalyzer.CounterDataPoint
                {
                    Timestamp = new DateTime(2025, 1, 1, 0, 0, 1, DateTimeKind.Utc),
                    Value = double.NaN
                }
            ]
        };

        var result = CounterLoadDataMapper.Map(
            @"\\PC\Memory\Available Bytes",
            counterInfo,
            _ => "Bytes",
            (value, unit) => $"{value:F1} {unit}");

        var mapped = Assert.Single(result.DataPoints);
        Assert.Equal(10.5, mapped.Value);
        Assert.Equal("Bytes", mapped.Unit);
        Assert.Equal("10.5 Bytes", mapped.FormattedValue);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Map_ReturnsNoErrorForCanceledEmptyResult()
    {
        var counterInfo = new BlgFileAnalyzer.CounterInfo
        {
            WasCanceled = true
        };

        var result = CounterLoadDataMapper.Map(
            @"\\PC\Processor(_Total)\% Processor Time",
            counterInfo,
            _ => "%",
            (value, unit) => $"{value:F1}{unit}");

        Assert.Empty(result.DataPoints);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Map_ReturnsNoErrorForCanceledNaNOnlyResult()
    {
        var counterInfo = new BlgFileAnalyzer.CounterInfo
        {
            WasCanceled = true,
            DataPoints =
            [
                new BlgFileAnalyzer.CounterDataPoint
                {
                    Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Value = double.NaN
                }
            ]
        };

        var result = CounterLoadDataMapper.Map(
            @"\\PC\Processor(_Total)\% Processor Time",
            counterInfo,
            _ => "%",
            (value, unit) => $"{value:F1}{unit}");

        Assert.Empty(result.DataPoints);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Map_ReturnsErrorForNonCanceledEmptyResult()
    {
        var result = CounterLoadDataMapper.Map(
            @"\\PC\Memory\Available Bytes",
            new BlgFileAnalyzer.CounterInfo(),
            _ => "Bytes",
            (value, unit) => $"{value:F1} {unit}");

        Assert.Empty(result.DataPoints);
        Assert.Equal(@"\\PC\Memory\Available Bytes: データポイントが見つかりませんでした", result.ErrorMessage);
    }
}
