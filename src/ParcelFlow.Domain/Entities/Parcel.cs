namespace ParcelFlow.Domain.Entities;

/// <summary>
/// The physical package a customer wants delivered. A parcel has exactly one
/// active <see cref="DeliveryTask"/> at a time (retries reuse the same task).
/// </summary>
public sealed class Parcel : TenantDocument
{
    /// <summary>Customer-facing tracking reference, unique per tenant.</summary>
    public string Reference { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public decimal WeightKg { get; set; }

    /// <summary>Cash-on-delivery amount. Zero when prepaid.</summary>
    public decimal CodAmount { get; set; }
}
