using System.Globalization;
using System.Text;

namespace ParcelFlow.Services.Reporting;

/// <summary>
/// Formats a weekly driver performance report as CSV. Five plain columns -
/// not worth a package dependency, so this is a small hand-rolled writer
/// instead.
/// </summary>
public static class WeeklyDriverPerformanceCsv
{
    private const string Header = "DriverId,DriverName,TasksDelivered,FailedAttempts,AvgHoursAssignedToDelivered";

    public static string Write(IEnumerable<WeeklyDriverPerformanceRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');

        foreach (var row in rows)
        {
            sb.Append(EscapeCsv(row.DriverId)).Append(',')
              .Append(EscapeCsv(row.DriverName)).Append(',')
              .Append(row.TasksDelivered.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(row.FailedAttempts.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(row.AvgHoursAssignedToDelivered.ToString("F2", CultureInfo.InvariantCulture))
              .Append('\n');
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
