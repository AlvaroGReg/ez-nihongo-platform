namespace EzNihongo.Platform.Api.Authentication;

public sealed class PersistentLoginOptions
{
    public const string SectionName = "PersistentLogin";
    public string[] AllowedOrigins { get; init; } = [];
    public bool AllowInsecureDevelopmentCookie { get; init; }

    public bool IsValid(string environmentName)
    {
        var originsValid = AllowedOrigins.Length > 0 && AllowedOrigins.All(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" &&
            uri.PathAndQuery == "/" &&
            string.IsNullOrEmpty(uri.Fragment) &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            origin == uri.GetLeftPart(UriPartial.Authority));

        return originsValid &&
            (!AllowInsecureDevelopmentCookie || string.Equals(environmentName, "Development", StringComparison.Ordinal));
    }
}
