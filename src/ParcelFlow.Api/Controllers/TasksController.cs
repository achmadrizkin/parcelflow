using LegacyCourier.Common;
using Microsoft.AspNetCore.Mvc;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Services;
using ParcelFlow.Storage;

namespace ParcelFlow.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<DeliveryTask> _taskRepo;
    private readonly DeliveryTaskService _tasks;
    private readonly AssignmentService _assignment;

    public TasksController(
        ITenantContext tenant,
        ITenantScopedRepository<DeliveryTask> taskRepo,
        DeliveryTaskService tasks,
        AssignmentService assignment)
    {
        _tenant = tenant;
        _taskRepo = taskRepo;
        _tasks = tasks;
        _assignment = assignment;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DeliveryTaskStatus? status, CancellationToken ct)
    {
        var result = status is null
            ? await _taskRepo.QueryAsync(_tenant.TenantId, t => true, ct)
            : await _taskRepo.QueryAsync(_tenant.TenantId, t => t.Status == status, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var task = await _taskRepo.GetAsync(_tenant.TenantId, id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>Assigns a specific driver, or auto-assigns when no driver id is given.</summary>
    [HttpPost("{id}/assign")]
    public async Task<IActionResult> Assign(string id, [FromBody] AssignRequest? request, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(request?.DriverId)
            ? await _assignment.AutoAssignAsync(id, ct)
            : await _tasks.AssignAsync(id, request!.DriverId!, ct);

        return ToResponse(result);
    }

    [HttpPost("{id}/pickup")]
    public async Task<IActionResult> Pickup(string id, CancellationToken ct)
        => ToResponse(await _tasks.RecordPickupAsync(id, ct));

    [HttpPost("{id}/start-transit")]
    public async Task<IActionResult> StartTransit(string id, CancellationToken ct)
        => ToResponse(await _tasks.StartTransitAsync(id, ct));

    [HttpPost("{id}/delivered")]
    public async Task<IActionResult> Delivered(string id, [FromBody] DeliveredRequest? request, CancellationToken ct)
        => ToResponse(await _tasks.MarkDeliveredAsync(id, request?.PodNote, ct));

    [HttpPost("{id}/attempt-failed")]
    public async Task<IActionResult> AttemptFailed(string id, [FromBody] AttemptFailedRequest request, CancellationToken ct)
        => ToResponse(await _tasks.RecordFailedAttemptAsync(id, request.Reason, ct));

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(string id, CancellationToken ct)
        => ToResponse(await _tasks.RetryAsync(id, ct));

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelRequest request, CancellationToken ct)
        => ToResponse(await _tasks.CancelAsync(id, request.Reason, ct));

    private IActionResult ToResponse(Result<DeliveryTask> result)
    {
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}

public sealed class AssignRequest
{
    public string? DriverId { get; init; }
}

public sealed class DeliveredRequest
{
    public string? PodNote { get; init; }
}

public sealed class AttemptFailedRequest
{
    public required string Reason { get; init; }
}

public sealed class CancelRequest
{
    public required string Reason { get; init; }
}
