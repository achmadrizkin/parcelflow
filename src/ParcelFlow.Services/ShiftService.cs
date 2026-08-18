using LegacyCourier.Common;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Storage;

namespace ParcelFlow.Services;

public sealed class ShiftService
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<Shift> _shifts;
    private readonly ITenantScopedRepository<Driver> _drivers;
    private readonly IClock _clock;

    public ShiftService(
        ITenantContext tenant,
        ITenantScopedRepository<Shift> shifts,
        ITenantScopedRepository<Driver> drivers,
        IClock clock)
    {
        _tenant = tenant;
        _shifts = shifts;
        _drivers = drivers;
        _clock = clock;
    }

    public async Task<Result<Shift>> StartShiftAsync(string driverId, CancellationToken ct = default)
    {
        var driver = await _drivers.GetAsync(_tenant.TenantId, driverId, ct);
        if (driver is null || !driver.IsActive)
        {
            return Result<Shift>.Fail($"Driver '{driverId}' not found or inactive.");
        }

        var openShifts = await _shifts.QueryAsync(
            _tenant.TenantId,
            s => s.DriverId == driverId && s.EndedUtc == null,
            ct);
        if (openShifts.Count > 0)
        {
            return Result<Shift>.Fail($"Driver '{driverId}' already has an open shift.");
        }

        var now = _clock.UtcNow;
        var shift = new Shift
        {
            Id = IdGenerator.NewId("shift"),
            TenantId = _tenant.TenantId,
            DriverId = driverId,
            StartedUtc = now,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await _shifts.UpsertAsync(shift, ct);
        return Result<Shift>.Ok(shift);
    }

    public async Task<Result<Shift>> EndShiftAsync(string driverId, CancellationToken ct = default)
    {
        var openShifts = await _shifts.QueryAsync(
            _tenant.TenantId,
            s => s.DriverId == driverId && s.EndedUtc == null,
            ct);
        if (openShifts.Count == 0)
        {
            return Result<Shift>.Fail($"Driver '{driverId}' has no open shift.");
        }

        var shift = openShifts[0];
        shift.EndedUtc = _clock.UtcNow;
        shift.UpdatedUtc = shift.EndedUtc.Value;

        await _shifts.UpsertAsync(shift, ct);
        return Result<Shift>.Ok(shift);
    }
}
