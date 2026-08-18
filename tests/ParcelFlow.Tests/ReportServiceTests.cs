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
}
