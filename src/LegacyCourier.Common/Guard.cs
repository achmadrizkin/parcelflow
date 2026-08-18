namespace LegacyCourier.Common;

public static class Guard
{
    public static string NotNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{paramName}' must not be null or empty.", paramName);
        }

        return value;
    }

    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }
}
