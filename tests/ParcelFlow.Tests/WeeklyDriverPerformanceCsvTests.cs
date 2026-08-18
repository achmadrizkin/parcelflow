using ParcelFlow.Services.Reporting;
using Xunit;

namespace ParcelFlow.Tests;

public class WeeklyDriverPerformanceCsvTests
{
    [Fact]
    public void Write_produces_a_header_and_one_row_per_driver()
    {
        var rows = new[]
        {
            new WeeklyDriverPerformanceRow
            {
                DriverId = "driver_1",
                DriverName = "Budi Santoso",
                TasksDelivered = 12,
                FailedAttempts = 3,
                AvgHoursAssignedToDelivered = 4.5
            }
        };

        var csv = WeeklyDriverPerformanceCsv.Write(rows);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("DriverId,DriverName,TasksDelivered,FailedAttempts,AvgHoursAssignedToDelivered", lines[0]);
        Assert.Equal("driver_1,Budi Santoso,12,3,4.50", lines[1]);
    }

    [Fact]
    public void Write_quotes_values_containing_commas()
    {
        var rows = new[]
        {
            new WeeklyDriverPerformanceRow
            {
                DriverId = "driver_2",
                DriverName = "Doe, Jane",
                TasksDelivered = 0,
                FailedAttempts = 0,
                AvgHoursAssignedToDelivered = 0
            }
        };

        var csv = WeeklyDriverPerformanceCsv.Write(rows);

        Assert.Contains("\"Doe, Jane\"", csv);
    }

    [Fact]
    public void Write_with_no_rows_is_just_the_header()
    {
        var csv = WeeklyDriverPerformanceCsv.Write(Array.Empty<WeeklyDriverPerformanceRow>());

        Assert.Equal("DriverId,DriverName,TasksDelivered,FailedAttempts,AvgHoursAssignedToDelivered\n", csv);
    }
}
