namespace EzNihongo.Platform.Api.Authentication;

public sealed class RegistrationLimitOptions
{
    public const string SectionName = "RegistrationLimit";

    public int DailyLimit { get; init; } = 3;
}
