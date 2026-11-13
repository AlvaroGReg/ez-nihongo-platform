namespace EzNihongo.Platform.Api.Authentication;

public sealed class PublicReadRateLimitOptions
{
    public const string SectionName = "PublicReadRateLimit";

    public int TokenLimit { get; init; } = 20;

    public int TokensPerPeriod { get; init; } = 20;

    public int ReplenishmentPeriodSeconds { get; init; } = 20;

    public bool IsValid() => TokenLimit > 0 && TokensPerPeriod > 0 && ReplenishmentPeriodSeconds > 0;
}
