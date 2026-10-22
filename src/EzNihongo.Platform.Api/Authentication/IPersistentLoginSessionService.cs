namespace EzNihongo.Platform.Api.Authentication;

public interface IPersistentLoginSessionService
{
    Task<PersistentLoginSessionResult> CreateAsync(string userId, CancellationToken cancellationToken);
    Task<PersistentLoginSessionResult> RotateAsync(string? token, CancellationToken cancellationToken);
    Task RevokeAsync(string? token, CancellationToken cancellationToken);
}

public sealed record PersistentLoginSessionResult(
    PersistentLoginSessionStatus Status,
    string? UserId = null,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null);

public enum PersistentLoginSessionStatus
{
    Invalid,
    Success,
    Reused
}
