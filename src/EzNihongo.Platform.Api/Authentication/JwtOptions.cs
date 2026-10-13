using System.Text;

namespace EzNihongo.Platform.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? SigningKey { get; init; }

    public string? Issuer { get; init; }

    public string? Audience { get; init; }

    public int AccessTokenMinutes { get; init; } = 15;

    public byte[] GetSigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey!);

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) >= 32 &&
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        AccessTokenMinutes > 0;
}
