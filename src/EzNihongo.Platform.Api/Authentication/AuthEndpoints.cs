using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace EzNihongo.Platform.Api.Authentication;

public static class AuthEndpoints
{
    private const string RefreshCookieName = "ez-nihongo-refresh";
    private const string RefreshCookiePath = "/api/v1/auth";
    private const string RequestHeaderName = "X-EZ-Nihongo-Request";

    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth");

        auth.MapPost("/register", async (AuthRequest request, IAuthService service, CancellationToken cancellationToken) =>
            {
                var result = await service.RegisterAsync(request, cancellationToken);
                if (result.IsConflict)
                    return (IResult)TypedResults.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Account already exists",
                        detail: "An account with this email address already exists.");
                if (result.Errors is not null)
                    return TypedResults.ValidationProblem(result.Errors);

                return TypedResults.Created("/api/v1/auth/me", result.Response!);
            })
            .WithName("Register")
            .WithSummary("Create an account and return an access token")
            .Produces<AuthTokenResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/login", async (AuthLoginRequest request, HttpContext context, IAuthService service,
            IPersistentLoginSessionService sessions, IOptions<PersistentLoginOptions> options,
            TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                var result = await service.LoginAsync(request, cancellationToken);
                if (result.Errors is not null)
                {
                    if (result.Errors.ContainsKey("credentials"))
                        return (IResult)TypedResults.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Unauthorized",
                            detail: "Invalid email or password.");

                    return TypedResults.ValidationProblem(result.Errors);
                }

                if (request.RememberMe)
                {
                    await sessions.RevokeAsync(context.Request.Cookies[RefreshCookieName], cancellationToken);
                    var session = await sessions.CreateAsync(result.Response!.User.Id, cancellationToken);
                    SetRefreshCookie(context, session.Token!, session.ExpiresAt!.Value, options.Value, timeProvider);
                }
                else
                {
                    await sessions.RevokeAsync(context.Request.Cookies[RefreshCookieName], cancellationToken);
                    ExpireRefreshCookie(context, options.Value);
                }

                return TypedResults.Ok(result.Response!);
            })
            .WithName("Login")
            .WithSummary("Authenticate and return an access token")
            .WithDescription("When rememberMe is true, sets a host-only HttpOnly persistent-session cookie for up to 30 days. Replaces any previous session presented by this browser. When false, revokes and expires any previous session cookie. The refresh token is never included in the response body.")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/refresh", async (HttpContext context, IAuthService authService,
            IPersistentLoginSessionService sessions, IOptions<PersistentLoginOptions> options,
            TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                if (!HasValidCookieRequest(context, options.Value))
                    return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden,
                        title: "Forbidden", detail: "The request origin or security headers are invalid.");
                if (!HasJsonContentType(context))
                    return TypedResults.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                        title: "Unsupported media type", detail: "Content-Type must be application/json.");

                var session = await sessions.RotateAsync(context.Request.Cookies[RefreshCookieName], cancellationToken);
                if (session.Status != PersistentLoginSessionStatus.Success)
                {
                    ExpireRefreshCookie(context, options.Value);
                    return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized", detail: "A valid persistent session is required.");
                }

                var response = await authService.CreateAccessTokenAsync(session.UserId!, cancellationToken);
                if (response is null)
                {
                    ExpireRefreshCookie(context, options.Value);
                    return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized", detail: "A valid persistent session is required.");
                }

                SetRefreshCookie(context, session.Token!, session.ExpiresAt!.Value, options.Value, timeProvider);
                return TypedResults.Ok(response);
            })
            .WithName("RefreshSession")
            .WithSummary("Rotate a persistent session and return a short-lived access token")
            .WithDescription("Requires application/json, an allowed Origin, X-EZ-Nihongo-Request: 1, and the HttpOnly ez-nihongo-refresh cookie. Rotates the cookie; the refresh token is never returned in the response body.")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", async (HttpContext context, IPersistentLoginSessionService sessions,
            IOptions<PersistentLoginOptions> options, CancellationToken cancellationToken) =>
            {
                if (!HasValidCookieRequest(context, options.Value))
                    return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden,
                        title: "Forbidden", detail: "The request origin or security headers are invalid.");
                if (!HasJsonContentType(context))
                    return TypedResults.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                        title: "Unsupported media type", detail: "Content-Type must be application/json.");

                await sessions.RevokeAsync(context.Request.Cookies[RefreshCookieName], cancellationToken);
                ExpireRefreshCookie(context, options.Value);
                return TypedResults.NoContent();
            })
            .WithName("Logout")
            .WithSummary("Revoke this browser's persistent session")
            .WithDescription("Requires application/json, an allowed Origin, X-EZ-Nihongo-Request: 1, and the HttpOnly ez-nihongo-refresh cookie. Revokes only that session family, expires the cookie, and remains idempotent when no session exists.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapGet("/api/v1/auth/me", (ClaimsPrincipal principal) =>
            {
                var id = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
                return TypedResults.Ok(new AuthIdentityResponse(id!, email!));
            })
            .WithName("GetCurrentIdentity")
            .WithTags("Authentication")
            .WithSummary("Return the identity represented by the bearer token")
            .RequireAuthorization()
            .Produces<AuthIdentityResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return auth;
    }

    private static bool HasValidCookieRequest(HttpContext context, PersistentLoginOptions options) =>
        context.Request.Method == HttpMethods.Post &&
        context.Request.Headers.Origin.Count == 1 &&
        options.AllowedOrigins.Contains(context.Request.Headers.Origin[0], StringComparer.Ordinal) &&
        context.Request.Headers[RequestHeaderName] == "1";

    private static bool HasJsonContentType(HttpContext context) =>
        string.Equals(context.Request.ContentType?.Split(';', 2)[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase);

    private static void SetRefreshCookie(HttpContext context, string token, DateTimeOffset expiresAt,
        PersistentLoginOptions options, TimeProvider timeProvider)
    {
        var remaining = expiresAt - timeProvider.GetUtcNow();
        var maxAge = remaining > TimeSpan.Zero
            ? TimeSpan.FromSeconds(Math.Floor(remaining.TotalSeconds))
            : TimeSpan.Zero;
        context.Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !options.AllowInsecureDevelopmentCookie,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = maxAge,
            Expires = expiresAt,
            IsEssential = true
        });
    }

    private static void ExpireRefreshCookie(HttpContext context, PersistentLoginOptions options) =>
        context.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !options.AllowInsecureDevelopmentCookie,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            IsEssential = true
        });
}
