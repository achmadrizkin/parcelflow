namespace LegacyCourier.Common;

/// <summary>
/// Generates short, sortable string ids. Carried over from LegacyCourier —
/// the whole platform stores ids as strings, do not switch to Guid fields.
/// </summary>
public static class IdGenerator
{
    public static string NewId(string prefix)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}_{timestamp}_{random}";
    }
}
