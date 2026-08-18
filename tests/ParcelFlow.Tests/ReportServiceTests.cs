using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Tests.TestHelpers;
using Xunit;

namespace ParcelFlow.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task Daily_summary_counts_delivered_tasks()
    {
        using var world = new TestWorld();
        var parcel = await world.SeedParcelAsync();
        var driver = await world.SeedDriverAsync();
        await world.SeedOpenShiftAsync(driver);
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;
        await world.TaskService.AssignAsync(task.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(task.Id);
        await world.TaskService.StartTransitAsync(task.Id);
        await world.TaskService.MarkDeliveredAsync(task.Id, null);

        var report = await world.ReportService.GetDailySummaryAsync(world.Clock.UtcNow.Date);

        Assert.Equal(1, report.TotalDelivered);
        var row = Assert.Single(report.Rows);
        Assert.Equal(parcel.Reference, row.ParcelReference);
        Assert.Equal(DeliveryTaskStatus.Delivered.ToString(), row.Status);
    }

    [Fact]
    public async Task Daily_summary_counts_failed_attempts()
    {
        using var world = new TestWorld();
        var parcel = await world.SeedParcelAsync();
        var driver = await world.SeedDriverAsync();
        await world.SeedOpenShiftAsync(driver);
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;
        await world.TaskService.AssignAsync(task.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(task.Id);
        await world.TaskService.StartTransitAsync(task.Id);
        await world.TaskService.RecordFailedAttemptAsync(task.Id, "recipient absent");

        var report = await world.ReportService.GetDailySummaryAsync(world.Clock.UtcNow.Date);

        Assert.Equal(1, report.TotalFailedAttempts);
    }
}
