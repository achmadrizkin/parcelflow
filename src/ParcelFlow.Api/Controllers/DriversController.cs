using LegacyCourier.Common;
using Microsoft.AspNetCore.Mvc;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Services;
using ParcelFlow.Storage;

namespace ParcelFlow.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<Driver> _drivers;
    private readonly ShiftService _shifts;
    private readonly IClock _clock;

    public DriversController(
        ITenantContext tenant,
        ITenantScopedRepository<Driver> drivers,
        ShiftService shifts,
        IClock clock)
    {
        _tenant = tenant;
        _drivers = drivers;
        _shifts = shifts;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return Ok(await _drivers.QueryAsync(_tenant.TenantId, d => true, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequest request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var driver = new Driver
        {
            Id = IdGenerator.NewId("driver"),
            TenantId = _tenant.TenantId,
            Name = request.Name,
            Phone = request.Phone ?? string.Empty,
            VehicleType = request.VehicleType ?? "motorbike",
            Capacity = request.Capacity ?? 10,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await _drivers.UpsertAsync(driver, ct);
        return Ok(driver);
    }

    [HttpPost("{id}/shifts/start")]
    public async Task<IActionResult> StartShift(string id, CancellationToken ct)
    {
        var result = await _shifts.StartShiftAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id}/shifts/end")]
    public async Task<IActionResult> EndShift(string id, CancellationToken ct)
    {
        var result = await _shifts.EndShiftAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}

public sealed class CreateDriverRequest
{
    public required string Name { get; init; }
    public string? Phone { get; init; }
    public string? VehicleType { get; init; }
    public int? Capacity { get; init; }
}
