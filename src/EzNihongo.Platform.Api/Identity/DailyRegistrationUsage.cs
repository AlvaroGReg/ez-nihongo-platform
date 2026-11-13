namespace EzNihongo.Platform.Api.Identity;

public sealed class DailyRegistrationUsage
{
    public DateOnly Date { get; set; }

    public int Count { get; set; }
}
