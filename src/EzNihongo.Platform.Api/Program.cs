using EzNihongo.Platform.Api.Vocabulary;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<VocabularyCatalog>();

var app = builder.Build();
_ = app.Services.GetRequiredService<VocabularyCatalog>();

app.UseExceptionHandler();
app.MapOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .WithTags("Health");

var vocabulary = app.MapGroup("/api/v1/vocabulary").WithTags("Vocabulary");

vocabulary.MapGet("/levels", (VocabularyCatalog catalog) =>
        TypedResults.Ok(catalog.GetLevelCounts()))
    .WithName("GetVocabularyLevels");

vocabulary.MapGet("/", (VocabularyCatalog catalog, int? level = null, int offset = 0, int limit = 50) =>
    {
        if (level is < 1 or > 5)
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid JLPT level",
                detail: "The level query parameter must be between 1 and 5.");
        }

        if (offset < 0 || limit is < 1 or > 200)
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid pagination",
                detail: "Offset must be zero or greater and limit must be between 1 and 200.");
        }

        return (IResult)TypedResults.Ok(catalog.GetPage(level, offset, limit));
    })
    .WithName("GetVocabulary");

vocabulary.MapGet("/{contentId}", (string contentId, VocabularyCatalog catalog) =>
    {
        var entry = catalog.Find(contentId);
        return entry is null
            ? (IResult)TypedResults.NotFound()
            : TypedResults.Ok(entry);
    })
    .WithName("GetVocabularyEntry");

app.Run();
