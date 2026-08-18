using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using Xunit;

namespace ParcelFlow.Tests;

public class DeliveryTaskStateMachineTests
{
    [Theory]
    [InlineData(DeliveryTaskStatus.Created, DeliveryTaskStatus.Assigned)]
    [InlineData(DeliveryTaskStatus.Created, DeliveryTaskStatus.Cancelled)]
    [InlineData(DeliveryTaskStatus.Assigned, DeliveryTaskStatus.PickedUp)]
    [InlineData(DeliveryTaskStatus.Assigned, DeliveryTaskStatus.Created)]
    [InlineData(DeliveryTaskStatus.Assigned, DeliveryTaskStatus.Cancelled)]
    [InlineData(DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.InTransit)]
    [InlineData(DeliveryTaskStatus.InTransit, DeliveryTaskStatus.Delivered)]
    [InlineData(DeliveryTaskStatus.InTransit, DeliveryTaskStatus.AttemptFailed)]
    [InlineData(DeliveryTaskStatus.AttemptFailed, DeliveryTaskStatus.InTransit)]
    [InlineData(DeliveryTaskStatus.AttemptFailed, DeliveryTaskStatus.Cancelled)]
    public void Allows_valid_transitions(DeliveryTaskStatus from, DeliveryTaskStatus to)
    {
        Assert.True(DeliveryTaskStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(DeliveryTaskStatus.Created, DeliveryTaskStatus.Delivered)]
    [InlineData(DeliveryTaskStatus.Created, DeliveryTaskStatus.InTransit)]
    [InlineData(DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.Delivered)]
    [InlineData(DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.Cancelled)]
    [InlineData(DeliveryTaskStatus.Delivered, DeliveryTaskStatus.InTransit)]
    [InlineData(DeliveryTaskStatus.Delivered, DeliveryTaskStatus.Cancelled)]
    [InlineData(DeliveryTaskStatus.Cancelled, DeliveryTaskStatus.Created)]
    [InlineData(DeliveryTaskStatus.InTransit, DeliveryTaskStatus.Cancelled)]
    public void Rejects_invalid_transitions(DeliveryTaskStatus from, DeliveryTaskStatus to)
    {
        Assert.False(DeliveryTaskStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void Terminal_states_have_no_exits()
    {
        Assert.True(DeliveryTaskStateMachine.IsTerminal(DeliveryTaskStatus.Delivered));
        Assert.True(DeliveryTaskStateMachine.IsTerminal(DeliveryTaskStatus.Cancelled));
        Assert.False(DeliveryTaskStateMachine.IsTerminal(DeliveryTaskStatus.Created));
        Assert.False(DeliveryTaskStateMachine.IsTerminal(DeliveryTaskStatus.AttemptFailed));
    }

    [Fact]
    public void Transition_records_history_and_updates_status()
    {
        var task = new DeliveryTask { Id = "task_1", TenantId = "t1", Status = DeliveryTaskStatus.Created };
        var at = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);

        DeliveryTaskStateMachine.Transition(task, DeliveryTaskStatus.Assigned, at, "test");

        Assert.Equal(DeliveryTaskStatus.Assigned, task.Status);
        Assert.Equal(at, task.UpdatedUtc);
        var change = Assert.Single(task.History);
        Assert.Equal(DeliveryTaskStatus.Created, change.From);
        Assert.Equal(DeliveryTaskStatus.Assigned, change.To);
        Assert.Equal("test", change.Reason);
    }

    [Fact]
    public void Transition_throws_and_leaves_task_untouched_on_invalid_jump()
    {
        var task = new DeliveryTask { Id = "task_1", TenantId = "t1", Status = DeliveryTaskStatus.Created };

        Assert.Throws<InvalidStateTransitionException>(() =>
            DeliveryTaskStateMachine.Transition(task, DeliveryTaskStatus.Delivered, DateTime.UtcNow));

        Assert.Equal(DeliveryTaskStatus.Created, task.Status);
        Assert.Empty(task.History);
    }
}
