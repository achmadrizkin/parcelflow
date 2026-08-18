namespace ParcelFlow.Domain.Entities;

/// <summary>
/// A working window for a driver. A driver is only assignable while they have
/// an open shift (StartedUtc set, EndedUtc null).
/// </summary>
public sealed class Shift : TenantDocument
{
    public string DriverId { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime? EndedUtc { get; set; }

    public bool IsOpen => EndedUtc is null;
}
