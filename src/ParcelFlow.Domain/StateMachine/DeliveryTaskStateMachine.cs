using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Domain.StateMachine;

/// <summary>
/// The single source of truth for the delivery task lifecycle.
///
///   Created ──► Assigned ──► PickedUp ──► InTransit ──► Delivered (terminal)
///      │            │                        │  ▲
///      ▼            ▼                        ▼  │ (retry)
///  Cancelled    Cancelled              AttemptFailed ──► Cancelled
///                   │
///                   └──► Created (unassign)
///
/// Any status change anywhere in the platform MUST go through
/// <see cref="Transition"/> so the audit history stays complete and invalid
/// jumps are impossible.
/// </summary>
public static class DeliveryTaskStateMachine
{
    private static readonly IReadOnlyDictionary<DeliveryTaskStatus, DeliveryTaskStatus[]> AllowedTransitions =
        new Dictionary<DeliveryTaskStatus, DeliveryTaskStatus[]>
        {
            [DeliveryTaskStatus.Created] = new[] { DeliveryTaskStatus.Assigned, DeliveryTaskStatus.Cancelled },
            [DeliveryTaskStatus.Assigned] = new[] { DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.Created, DeliveryTaskStatus.Cancelled },
            [DeliveryTaskStatus.PickedUp] = new[] { DeliveryTaskStatus.InTransit },
            [DeliveryTaskStatus.InTransit] = new[] { DeliveryTaskStatus.Delivered, DeliveryTaskStatus.AttemptFailed },
            [DeliveryTaskStatus.AttemptFailed] = new[] { DeliveryTaskStatus.InTransit, DeliveryTaskStatus.Cancelled },
            [DeliveryTaskStatus.Delivered] = Array.Empty<DeliveryTaskStatus>(),
            [DeliveryTaskStatus.Cancelled] = Array.Empty<DeliveryTaskStatus>()
        };

    public static bool CanTransition(DeliveryTaskStatus from, DeliveryTaskStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static bool IsTerminal(DeliveryTaskStatus status)
    {
        return AllowedTransitions.TryGetValue(status, out var targets) && targets.Length == 0;
    }

    /// <summary>
    /// Applies a status change to the task, recording it in the audit history.
    /// Throws <see cref="InvalidStateTransitionException"/> for illegal jumps.
    /// </summary>
    public static void Transition(DeliveryTask task, DeliveryTaskStatus to, DateTime atUtc, string? reason = null)
    {
        if (!CanTransition(task.Status, to))
        {
            throw new InvalidStateTransitionException(task.Id, task.Status, to);
        }

        task.History.Add(new StatusChange
        {
            From = task.Status,
            To = to,
            AtUtc = atUtc,
            Reason = reason
        });

        task.Status = to;
        task.UpdatedUtc = atUtc;
    }
}
