using System.Security.Claims;
using EzNihongo.Platform.Api.Vocabulary;

namespace EzNihongo.Platform.Api.VocabularyProgress;

public static class VocabularyProgressEndpoints
{
    public static WebApplication MapVocabularyProgressEndpoints(this WebApplication app)
    {
        var progress = app.MapGroup("/api/v1/progress/vocabulary")
            .WithTags("Vocabulary progress")
            .RequireAuthorization();

        progress.MapPost("/attempts", async Task<IResult> (
                CreateVocabularyAttemptRequest request,
                ClaimsPrincipal principal,
                VocabularyCatalog catalog,
                IVocabularyProgressService service,
                CancellationToken cancellationToken) =>
            {
                var userId = GetUserId(principal);
                if (userId is null)
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "The authenticated user identity is unavailable.");

                if (request.ExerciseType != "reading" || catalog.Find(request.ContentId, "en") is null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.ContentId)] = ["A valid vocabulary content ID and exercise type are required."]
                    });
                }

                await service.RecordAttemptAsync(userId, request, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RecordVocabularyAttempt")
            .WithSummary("Record a vocabulary attempt for the authenticated user")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        progress.MapGet("/attempts", async Task<IResult> (
                ClaimsPrincipal principal,
                IVocabularyProgressService service,
                int offset = 0,
                int limit = 50,
                CancellationToken cancellationToken = default) =>
            {
                var userId = GetUserId(principal);
                if (userId is null)
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "The authenticated user identity is unavailable.");

                if (offset < 0 || limit is < 1 or > 200)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid pagination",
                        detail: "Offset must be zero or greater and limit must be between 1 and 200.");
                }

                var page = await service.GetAttemptsAsync(userId, offset, limit, cancellationToken);
                return TypedResults.Ok(page);
            })
            .WithName("GetVocabularyAttempts")
            .WithSummary("Get the authenticated user's vocabulary attempt history")
            .Produces<VocabularyAttemptPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
