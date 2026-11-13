namespace EzNihongo.Platform.Api.Authentication;

public interface IAuthService
{
    Task<AuthOperationResult> RegisterAsync(AuthRequest request, CancellationToken cancellationToken);

    Task<AuthOperationResult> LoginAsync(AuthLoginRequest request, CancellationToken cancellationToken);

    Task<AuthTokenResponse?> CreateAccessTokenAsync(string userId, CancellationToken cancellationToken);
}

public sealed record AuthOperationResult(
    AuthTokenResponse? Response,
    IReadOnlyDictionary<string, string[]>? Errors,
    bool IsConflict,
    bool IsRateLimited)
{
    public static AuthOperationResult Success(AuthTokenResponse response) => new(response, null, false, false);

    public static AuthOperationResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors, false, false);

    public static AuthOperationResult Conflict() => new(null, null, true, false);

    public static AuthOperationResult RateLimited() => new(null, null, false, true);
}
