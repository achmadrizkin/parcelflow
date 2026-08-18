using Microsoft.Extensions.Logging.Abstractions;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.Events;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Events;
using ParcelFlow.Events.Actions;
using ParcelFlow.Events.Rules;
using ParcelFlow.Services;
using ParcelFlow.Tests.TestHelpers;
using Xunit;

namespace ParcelFlow.Tests;

public class ReturnToSenderTests
{
    private static ScheduleReturnOnThirdFailedAttemptRule CreateRule(TestWorld world)
    {
        return new ScheduleReturnOnThirdFailedAttemptRule(
            world.Tasks,
            world.Parcels,
            new SmsNotificationAction(NullLogger<SmsNotificationAction>.Instance),
            new OpsWebhookAction(NullLogger<OpsWebhookAction>.Instance));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void AppliesTo_only_from_the_third_failed_attempt(int attemptNumber, bool expected)
    {
        using var world = new TestWorld();
        var rule = CreateRule(world);
        var evt = new DeliveryAttemptFailedEvent
        {
            TenantId = world.TenantId,
            OccurredUtc = world.Clock.UtcNow,
            Task = new DeliveryTask { Id = "task_1", TenantId = world.TenantId },
            AttemptNumber = attemptNumber,
            Reason = "test"
        };

        Assert.Equal(expected, rule.AppliesTo(evt));
    }

    [Fact]
    public async Task ExecuteAsync_transitions_task_to_ReturnScheduled_and_persists_it()
    {
        using var world = new TestWorld();
        var parcel = await world.SeedParcelAsync();
        var driver = await world.SeedDriverAsync();
        await world.SeedOpenShiftAsync(driver);
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!;
        await world.TaskService.AssignAsync(task.Id, driver.Id);
        await world.TaskService.RecordPickupAsync(task.Id);
        await world.TaskService.StartTransitAsync(task.Id);
        var failed = (await world.TaskService.RecordFailedAttemptAsync(task.Id, "recipient absent")).Value!;

        var rule = CreateRule(world);
        var evt = new DeliveryAttemptFailedEvent
        {
            TenantId = world.TenantId,
            OccurredUtc = world.Clock.UtcNow,
            Task = failed,
            AttemptNumber = 3,
            Reason = "recipient absent"
        };

        await rule.ExecuteAsync(evt, CancellationToken.None);

        var persisted = await world.Tasks.GetAsync(world.TenantId, task.Id);
        Assert.Equal(DeliveryTaskStatus.ReturnScheduled, persisted!.Status);
        Assert.NotNull(persisted.ReturnScheduledUtc);
    }

    [Fact]
    public async Task ExecuteAsync_is_a_no_op_when_the_task_is_not_in_AttemptFailed_state()
    {
        using var world = new TestWorld();
        var parcel = await world.SeedParcelAsync();
        var task = (await world.TaskService.CreateForParcelAsync(parcel.Id)).Value!; // status: Created

        var rule = CreateRule(world);
        var evt = new DeliveryAttemptFailedEvent
        {
            TenantId = world.TenantId,
            OccurredUtc = world.Clock.UtcNow,
            Task = task,
            AttemptNumber = 3,
            Reason = "test"
        };

        await rule.ExecuteAsync(evt, CancellationToken.None);

        var persisted = await world.Tasks.GetAsync(world.TenantId, task.Id);
        Assert.Equal(DeliveryTaskStatus.Created, persisted!.Status);
    }

    /// <summary>
    /// Drives three real failed attempts through DeliveryTaskService wired to
    /// a real EventDispatcher (unlike TestWorld's default recording
    /// dispatcher), proving the "3rd attempt schedules a return" behaviour
    /// is automatic through the actual event pipeline, not just achievable
    /// by calling the rule directly.
    /// </summary>
    [Fact]
    public async Task Third_failed_attempt_automatically_schedules_return_through_the_real_event_pipeline()
    {
        using var world = new TestWorld();
        var parcel = await world.SeedParcelAsync();
        var driver = await world.SeedDriverAsync();
        await world.SeedOpenShiftAsync(driver);

        var rule = CreateRule(world);
        var dispatcher = new EventDispatcher(new IEventRule[] { rule }, NullLogger<EventDispatcher>.Instance);
        var taskService = new DeliveryTaskService(
            world.TenantContext, world.Tasks, world.Parcels, world.Drivers, world.Shifts, dispatcher, world.Clock);

        var task = (await taskService.CreateForParcelAsync(parcel.Id)).Value!;
        await taskService.AssignAsync(task.Id, driver.Id);
        await taskService.RecordPickupAsync(task.Id);
        await taskService.StartTransitAsync(task.Id);

        await taskService.RecordFailedAttemptAsync(task.Id, "recipient absent");
        await taskService.RetryAsync(task.Id);
        await taskService.RecordFailedAttemptAsync(task.Id, "recipient absent");
        await taskService.RetryAsync(task.Id);
        var third = await taskService.RecordFailedAttemptAsync(task.Id, "recipient absent");

        Assert.True(third.IsSuccess);
        Assert.Equal(DeliveryTaskStatus.ReturnScheduled, third.Value!.Status);
        Assert.Equal(3, third.Value.AttemptCount);

        var persisted = await world.Tasks.GetAsync(world.TenantId, task.Id);
        Assert.Equal(DeliveryTaskStatus.ReturnScheduled, persisted!.Status);
    }

    /// <summary>
    /// Two tenants share the same store (as in production). Tenant A's task
    /// reaches its 3rd failed attempt; tenant B's only reaches its 1st. The
    /// automatic return must only ever touch the owning tenant's task.
    /// </summary>
    [Fact]
    public async Task Multi_tenant_automatic_return_only_affects_the_owning_tenants_task()
    {
        using var world = new TestWorld();
        var rule = CreateRule(world);
        var dispatcher = new EventDispatcher(new IEventRule[] { rule }, NullLogger<EventDispatcher>.Instance);

        var tenantAContext = new TenantContext();
        tenantAContext.SetTenant("tenant-a");
        var tenantBContext = new TenantContext();
        tenantBContext.SetTenant("tenant-b");

        var taskServiceA = new DeliveryTaskService(tenantAContext, world.Tasks, world.Parcels, world.Drivers, world.Shifts, dispatcher, world.Clock);
        var taskServiceB = new DeliveryTaskService(tenantBContext, world.Tasks, world.Parcels, world.Drivers, world.Shifts, dispatcher, world.Clock);

        var parcelA = await world.SeedParcelAsync(tenantId: "tenant-a");
        var driverA = await world.SeedDriverAsync(tenantId: "tenant-a");
        await world.SeedOpenShiftAsync(driverA);

        var parcelB = await world.SeedParcelAsync(tenantId: "tenant-b");
        var driverB = await world.SeedDriverAsync(tenantId: "tenant-b");
        await world.SeedOpenShiftAsync(driverB);

        var taskA = (await taskServiceA.CreateForParcelAsync(parcelA.Id)).Value!;
        await taskServiceA.AssignAsync(taskA.Id, driverA.Id);
        await taskServiceA.RecordPickupAsync(taskA.Id);
        await taskServiceA.StartTransitAsync(taskA.Id);

        var taskB = (await taskServiceB.CreateForParcelAsync(parcelB.Id)).Value!;
        await taskServiceB.AssignAsync(taskB.Id, driverB.Id);
        await taskServiceB.RecordPickupAsync(taskB.Id);
        await taskServiceB.StartTransitAsync(taskB.Id);

        await taskServiceA.RecordFailedAttemptAsync(taskA.Id, "recipient absent");
        await taskServiceA.RetryAsync(taskA.Id);
        await taskServiceA.RecordFailedAttemptAsync(taskA.Id, "recipient absent");
        await taskServiceA.RetryAsync(taskA.Id);
        await taskServiceA.RecordFailedAttemptAsync(taskA.Id, "recipient absent");

        await taskServiceB.RecordFailedAttemptAsync(taskB.Id, "recipient absent");

        var persistedA = await world.Tasks.GetAsync("tenant-a", taskA.Id);
        var persistedB = await world.Tasks.GetAsync("tenant-b", taskB.Id);

        Assert.Equal(DeliveryTaskStatus.ReturnScheduled, persistedA!.Status);
        Assert.Equal(DeliveryTaskStatus.AttemptFailed, persistedB!.Status);
    }
}
