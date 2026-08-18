using Microsoft.AspNetCore.Mvc;
using ParcelFlow.Services;

namespace ParcelFlow.Api.Controllers;

[ApiController]
[Route("api/parcels")]
public sealed class ParcelsController : ControllerBase
{
    private readonly ParcelService _parcels;
    private readonly DeliveryTaskService _tasks;

    public ParcelsController(ParcelService parcels, DeliveryTaskService tasks)
    {
        _parcels = parcels;
        _tasks = tasks;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return Ok(await _parcels.ListAsync(ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var parcel = await _parcels.GetAsync(id, ct);
        return parcel is null ? NotFound() : Ok(parcel);
    }

    /// <summary>Registers a parcel and immediately opens a delivery task for it.</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterParcelRequest request, CancellationToken ct)
    {
        var parcelResult = await _parcels.RegisterAsync(
            request.Reference,
            request.RecipientName,
            request.RecipientPhone ?? string.Empty,
            request.AddressLine ?? string.Empty,
            request.City ?? string.Empty,
            request.WeightKg,
            request.CodAmount,
            ct);

        if (!parcelResult.IsSuccess)
        {
            return BadRequest(new { error = parcelResult.Error });
        }

        var taskResult = await _tasks.CreateForParcelAsync(parcelResult.Value!.Id, ct);
        if (!taskResult.IsSuccess)
        {
            return BadRequest(new { error = taskResult.Error });
        }

        return Ok(new { parcel = parcelResult.Value, task = taskResult.Value });
    }
}

public sealed class RegisterParcelRequest
{
    public required string Reference { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public decimal WeightKg { get; init; }
    public decimal CodAmount { get; init; }
}
