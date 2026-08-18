using LegacyCourier.Common;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Storage;

namespace ParcelFlow.Services;

public sealed class ParcelService
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly IClock _clock;

    public ParcelService(
        ITenantContext tenant,
        ITenantScopedRepository<Parcel> parcels,
        IClock clock)
    {
        _tenant = tenant;
        _parcels = parcels;
        _clock = clock;
    }

    public async Task<Result<Parcel>> RegisterAsync(
        string reference,
        string recipientName,
        string recipientPhone,
        string addressLine,
        string city,
        decimal weightKg,
        decimal codAmount,
        CancellationToken ct = default)
    {
        Guard.NotNullOrEmpty(reference, nameof(reference));
        Guard.NotNullOrEmpty(recipientName, nameof(recipientName));

        var duplicates = await _parcels.QueryAsync(_tenant.TenantId, p => p.Reference == reference, ct);
        if (duplicates.Count > 0)
        {
            return Result<Parcel>.Fail($"A parcel with reference '{reference}' already exists.");
        }

        var now = _clock.UtcNow;
        var parcel = new Parcel
        {
            Id = IdGenerator.NewId("parcel"),
            TenantId = _tenant.TenantId,
            Reference = reference,
            RecipientName = recipientName,
            RecipientPhone = recipientPhone,
            AddressLine = addressLine,
            City = city,
            WeightKg = weightKg,
            CodAmount = codAmount,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await _parcels.UpsertAsync(parcel, ct);
        return Result<Parcel>.Ok(parcel);
    }

    public Task<Parcel?> GetAsync(string parcelId, CancellationToken ct = default)
    {
        return _parcels.GetAsync(_tenant.TenantId, parcelId, ct);
    }

    public Task<IReadOnlyList<Parcel>> ListAsync(CancellationToken ct = default)
    {
        return _parcels.QueryAsync(_tenant.TenantId, p => true, ct);
    }
}
