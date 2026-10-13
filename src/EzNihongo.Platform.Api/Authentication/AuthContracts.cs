using System.ComponentModel.DataAnnotations;

namespace EzNihongo.Platform.Api.Authentication;

/// <summary>Credentials used to create an account or start a session.</summary>
public sealed record AuthRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

/// <summary>Public identity details for the authenticated account.</summary>
public sealed record AuthUserResponse(string Id, string Email);

/// <summary>Identity and bearer access token returned after authentication.</summary>
public sealed record AuthTokenResponse(
    AuthUserResponse User,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt);

/// <summary>Public identity associated with the current access token.</summary>
public sealed record AuthIdentityResponse(string Id, string Email);
