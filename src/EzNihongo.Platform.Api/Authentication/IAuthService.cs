namespace EzNihongo.Platform.Api.Authentication;

public interface IAuthService
{
    Task<AuthOperationResult> RegisterAsync(AuthRequest request, CancellationToken cancellationToken);

    Task<AuthOperationResult> LoginAsync(AuthRequest request, CancellationToken cancellationToken);
}

public sealed record AuthOperationResult(
    AuthTokenResponse? Response,
    IReadOnlyDictionary<string, string[]>? Errors,
    bool IsConflict)
{
    public static AuthOperationResult Success(AuthTokenResponse response) => new(response, null, false);

    public static AuthOperationResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors, false);

    public static AuthOperationResult Conflict() => new(null, null, true);
}
