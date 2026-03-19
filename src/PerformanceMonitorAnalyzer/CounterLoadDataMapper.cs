namespace PerformanceMonitorAnalyzer;

internal static class CounterLoadDataMapper
{
    internal sealed class MappingResult
    {
        public required List<PerformanceDataPoint> DataPoints { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public static MappingResult Map(
        string counterPath,
        BlgFileAnalyzer.CounterInfo counterInfo,
        Func<string, string> estimateUnit,
        Func<double, string, string> formatValueWithUnit)
    {
        if (counterInfo.DataPoints.Count == 0)
        {
            return new MappingResult
            {
                DataPoints = [],
                ErrorMessage = counterInfo.WasCanceled
                    ? null
                    : $"{counterPath}: データポイントが見つかりませんでした"
            };
        }

        var unit = estimateUnit(counterPath);
        var mappedPoints = new List<PerformanceDataPoint>();

        foreach (var dataPoint in counterInfo.DataPoints)
        {
            if (double.IsNaN(dataPoint.Value))
            {
                continue;
            }

            mappedPoints.Add(new PerformanceDataPoint
            {
                Counter = counterPath,
                Value = dataPoint.Value,
                Timestamp = dataPoint.Timestamp,
                FormattedValue = formatValueWithUnit(dataPoint.Value, unit),
                Unit = unit
            });
        }

        if (mappedPoints.Count > 0)
        {
            return new MappingResult
            {
                DataPoints = mappedPoints
            };
        }

        return new MappingResult
        {
            DataPoints = [],
            ErrorMessage = counterInfo.WasCanceled
                ? null
                : $"{counterPath}: 有効なデータポイントが見つかりませんでした"
        };
    }
}
