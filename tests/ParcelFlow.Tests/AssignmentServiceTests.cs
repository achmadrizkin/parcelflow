using ParcelFlow.Tests.TestHelpers;
using Xunit;

namespace ParcelFlow.Tests;

public class AssignmentServiceTests
{
    [Fact]
    public async Task Assigns_to_driver_with_most_spare_capacity_on_open_shift()
    {
        using var world = new TestWorld();
        var busyDriver = await world.SeedDriverAsync(name: "Busy", capacity: 2);
        var freeDriver = await world.SeedDriverAsync(name: "Free", capacity: 10);
        await world.SeedOpenShiftAsync(busyDriver);
        await world.SeedOpenShiftAsync(freeDriver);

        var parcel = await world.SeedParcelAsync();
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;

        var result = await world.AssignmentService.AutoAssignAsync(task.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(freeDriver.Id, result.Value!.DriverId);
    }

    [Fact]
    public async Task Fails_when_no_driver_is_on_shift()
    {
        using var world = new TestWorld();
        await world.SeedDriverAsync(); // exists but has no open shift

        var parcel = await world.SeedParcelAsync();
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;

        var result = await world.AssignmentService.AutoAssignAsync(task.Id);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Never_assigns_a_driver_from_another_tenant()
    {
        using var world = new TestWorld();
        var foreignDriver = await world.SeedDriverAsync(tenantId: "other-tenant", name: "Foreign");
        await world.SeedOpenShiftAsync(foreignDriver);

        var parcel = await world.SeedParcelAsync();
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;

        var result = await world.AssignmentService.AutoAssignAsync(task.Id);

        Assert.False(result.IsSuccess);
    }
}
