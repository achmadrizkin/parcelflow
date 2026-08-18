namespace ParcelFlow.Domain.StateMachine;

public sealed class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string taskId, DeliveryTaskStatus from, DeliveryTaskStatus to)
        : base($"Task '{taskId}' cannot transition from {from} to {to}.")
    {
        TaskId = taskId;
        From = from;
        To = to;
    }

    public string TaskId { get; }
    public DeliveryTaskStatus From { get; }
    public DeliveryTaskStatus To { get; }
}
