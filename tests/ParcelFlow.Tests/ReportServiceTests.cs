using LegacyCourier.Common;
using ParcelFlow.Domain.Entities;
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

    /// <summary>
    /// Regression test for PF-1287: the daily summary must never surface
    /// another tenant's parcels, cities, or drivers. Reproduces the ticket's
    /// exact symptoms (a foreign city, a foreign driver name) with a second
    /// tenant's data present in the shared store.
    /// </summary>
    [Fact]
    public async Task Daily_summary_never_includes_another_tenants_data()
    {
        using var world = new TestWorld();

        var parcelA = await world.SeedParcelAsync(reference: "NE-A-001", city: "Jakarta");
        var driverA = await world.SeedDriverAsync(name: "Driver A");
        await world.SeedOpenShiftAsync(driverA);
        var taskA = (await world.TaskService.CreateForParcelAsync(parcelA.Id)).Value!;
        await world.TaskService.AssignAsync(taskA.Id, driverA.Id);
        await world.TaskService.RecordPickupAsync(taskA.Id);
        await world.TaskService.StartTransitAsync(taskA.Id);
        await world.TaskService.MarkDeliveredAsync(taskA.Id, null);

        const string otherTenantId = "other-tenant";
        var parcelB = await world.SeedParcelAsync(tenantId: otherTenantId, reference: "MS-B-999", city: "Manila");
        var driverB = await world.SeedDriverAsync(tenantId: otherTenantId, name: "Driver B");
        var taskB = new DeliveryTask
        {
            Id = IdGenerator.NewId("task"),
            TenantId = otherTenantId,
            ParcelId = parcelB.Id,
            DriverId = driverB.Id,
            Status = DeliveryTaskStatus.Delivered,
            CreatedUtc = world.Clock.UtcNow,
            UpdatedUtc = world.Clock.UtcNow,
            DeliveredUtc = world.Clock.UtcNow
        };
        await world.Tasks.UpsertAsync(taskB);

        var report = await world.ReportService.GetDailySummaryAsync(world.Clock.UtcNow.Date);

        Assert.Equal(1, report.TotalDelivered);
        var row = Assert.Single(report.Rows);
        Assert.Equal(parcelA.Reference, row.ParcelReference);
        Assert.DoesNotContain(report.Rows, r => r.ParcelReference == parcelB.Reference);
        Assert.DoesNotContain(report.Rows, r => r.RecipientCity == "Manila");
        Assert.DoesNotContain(report.Rows, r => r.DriverName == driverB.Name);
    }

    [Fact]
    public async Task Weekly_driver_performance_counts_only_events_within_the_trailing_7_days()
    {
        using var world = new TestWorld();
        var driver = await world.SeedDriverAsync(name: "Driver A");
        await world.SeedOpenShiftAsync(driver);

        // asOf 2026-07-15 -> window is [2026-07-08, 2026-07-15).
        var asOf = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        // In-window: delivered 5 hours after assignment.
        world.Clock.UtcNow = new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
        var inWindowDelivered = await world.SeedParcelAsync(reference: "IN-DELIVERED");
        var deliveredTask = (await world.TaskService.CreateForParcelAsync(inWindowDelivered.Id)).Value!;
        await world.TaskService.AssignAsync(deliveredTask.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(deliveredTask.Id);
        await world.TaskService.StartTransitAsync(deliveredTask.Id);
        world.Clock.UtcNow = world.Clock.UtcNow.AddHours(5);
        await world.TaskService.MarkDeliveredAsync(deliveredTask.Id, null);

        // In-window: one failed attempt.
        world.Clock.UtcNow = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var inWindowFailed = await world.SeedParcelAsync(reference: "IN-FAILED");
        var failedTask = (await world.TaskService.CreateForParcelAsync(inWindowFailed.Id)).Value!;
        await world.TaskService.AssignAsync(failedTask.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(failedTask.Id);
        await world.TaskService.StartTransitAsync(failedTask.Id);
        await world.TaskService.RecordFailedAttemptAsync(failedTask.Id, "recipient absent");

        // Outside the window: delivered a week before it opens.
        world.Clock.UtcNow = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var outOfWindow = await world.SeedParcelAsync(reference: "OUT-OF-WINDOW");
        var oldTask = (await world.TaskService.CreateForParcelAsync(outOfWindow.Id)).Value!;
        await world.TaskService.AssignAsync(oldTask.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(oldTask.Id);
        await world.TaskService.StartTransitAsync(oldTask.Id);
        await world.TaskService.MarkDeliveredAsync(oldTask.Id, null);

        var rows = await world.ReportService.GetWeeklyDriverPerformanceAsync(asOf);

        var row = Assert.Single(rows);
        Assert.Equal(driver.Id, row.DriverId);
        Assert.Equal(driver.Name, row.DriverName);
        Assert.Equal(1, row.TasksDelivered);
        Assert.Equal(1, row.FailedAttempts);
        Assert.Equal(5.0, row.AvgHoursAssignedToDelivered);
    }
}
