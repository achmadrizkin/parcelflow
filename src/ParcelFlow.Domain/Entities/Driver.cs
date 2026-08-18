namespace ParcelFlow.Domain.Entities;

public sealed class Driver : TenantDocument
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string VehicleType { get; set; } = "motorbike";

    /// <summary>Maximum number of tasks the driver can hold at once.</summary>
    public int Capacity { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}
