namespace EzNihongo.Platform.Api.Authentication;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimit";

    public int PermitLimit { get; init; } = 5;

    public int WindowMinutes { get; init; } = 1;

    public bool IsValid() => PermitLimit > 0 && WindowMinutes > 0;
}
