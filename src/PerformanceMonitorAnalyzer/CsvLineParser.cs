using System.Text;

namespace PerformanceMonitorAnalyzer;

public static class CsvLineParser
{
    public static List<string> Parse(string csvLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < csvLine.Length; i++)
        {
            var c = csvLine[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < csvLine.Length && csvLine[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
