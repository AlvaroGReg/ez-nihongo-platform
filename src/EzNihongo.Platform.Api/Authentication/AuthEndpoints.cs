using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace EzNihongo.Platform.Api.Authentication;

public static class AuthEndpoints
{
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

        auth.MapPost("/login", async (AuthRequest request, IAuthService service, CancellationToken cancellationToken) =>
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

                return TypedResults.Ok(result.Response!);
            })
            .WithName("Login")
            .WithSummary("Authenticate and return an access token")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
}
