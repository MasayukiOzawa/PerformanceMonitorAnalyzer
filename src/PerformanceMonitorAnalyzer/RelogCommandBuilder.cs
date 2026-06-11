using System.IO;
using System.Text;

namespace PerformanceMonitorAnalyzer;

public static class RelogCommandBuilder
{
    public static string Build(string blgFilePath, DateTime effectiveStartTime, DateTime effectiveEndTime)
    {
        var commandBuilder = new StringBuilder();
        commandBuilder.AppendLine("relog.exe `");
        commandBuilder.AppendLine($"  \"{blgFilePath}\" `");

        var outputFileName = Path.GetFileNameWithoutExtension(blgFilePath) + "_output.blg";
        commandBuilder.AppendLine($"  -o \"{outputFileName}\" `");
        commandBuilder.AppendLine("  -f BIN `");
        commandBuilder.AppendLine($"  -b \"{effectiveStartTime:yyyy/MM/dd HH:mm:ss}\" `");
        commandBuilder.Append($"  -e \"{effectiveEndTime:yyyy/MM/dd HH:mm:ss}\"");

        return commandBuilder.ToString();
    }
}
