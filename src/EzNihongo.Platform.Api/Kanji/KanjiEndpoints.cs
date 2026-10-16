namespace EzNihongo.Platform.Api.Kanji;

public static class KanjiEndpoints
{
    public static WebApplication MapKanjiEndpoints(this WebApplication app)
    {
        var kanji = app.MapGroup("/api/v1/kanji").WithTags("Kanji");

        kanji.MapGet("/levels", (KanjiCatalog catalog) => TypedResults.Ok(catalog.GetCounts()))
            .WithName("GetKanjiCounts")
            .WithSummary("Get JLPT kanji counts and the additional kanji count");

        kanji.MapGet("/", (KanjiCatalog catalog, int? level = null, string? collection = null,
            int offset = 0, int limit = 50) =>
        {
            if (level is < 1 or > 5)
                return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid JLPT level", detail: "The level query parameter must be between 1 and 5.");
            if (collection is not (null or "jlpt" or "extra"))
                return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid collection", detail: "The collection query parameter must be 'jlpt' or 'extra'.");
            if (collection == "extra" && level.HasValue)
                return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid kanji filters", detail: "Extra kanji do not have a JLPT level.");
            if (offset < 0 || limit is < 1 or > 200)
                return (IResult)TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid pagination", detail: "Offset must be zero or greater and limit must be between 1 and 200.");

            return (IResult)TypedResults.Ok(catalog.GetPage(level, collection, offset, limit));
        })
            .WithName("GetKanji")
            .WithSummary("List kanji with optional JLPT-level or collection filters")
            .WithDescription("Use collection=extra to retrieve kanji outside the JLPT lists; omit filters to include all kanji.");

        kanji.MapGet("/{id}", (string id, KanjiCatalog catalog) =>
            {
                var entry = catalog.Find(id);
                return entry is null ? (IResult)TypedResults.NotFound() : TypedResults.Ok(entry);
            })
            .WithName("GetKanjiById")
            .WithSummary("Get one kanji by its stable catalog ID")
            .Produces<KanjiRecord>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
