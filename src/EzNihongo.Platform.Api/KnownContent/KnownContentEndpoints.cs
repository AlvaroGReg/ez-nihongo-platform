using System.Security.Claims;
using System.Text;
using EzNihongo.Platform.Api.Kanji;
using EzNihongo.Platform.Api.Vocabulary;

namespace EzNihongo.Platform.Api.KnownContent;

public static class KnownContentEndpoints
{
    public static WebApplication MapKnownContentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/me/known-content")
            .WithTags("Known content")
            .RequireAuthorization();

        group.MapGet("/", async Task<IResult> (
                ClaimsPrincipal principal, IKnownContentService service,
                VocabularyCatalog vocabulary, KanjiCatalog kanji,
                string? type = null, int offset = 0, int limit = 50, string language = "en",
                CancellationToken cancellationToken = default) =>
            {
                if (!IsValidType(type) || offset < 0 || limit is < 1 or > 200 || !IsValidLanguage(language))
                    return InvalidQuery();
                var userId = GetUserId(principal);
                if (userId is null) return UnauthorizedIdentity();
                var page = await service.GetPageAsync(userId, type, offset, limit, cancellationToken);
                return TypedResults.Ok(new KnownContentPageResponse(page.Total, offset, limit,
                    page.Items.Select(item => ToResponse(item, language, vocabulary, kanji)).ToArray()));
            })
            .WithName("GetMyKnownContent")
            .WithSummary("Get a page of the authenticated user's known content")
            .WithDescription("Returns only entries owned by the bearer-token subject, ordered newest first with stable ID tie-breaking.")
            .Produces<KnownContentPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/catalog", async Task<IResult> (
                AddKnownCatalogRequest request, ClaimsPrincipal principal, IKnownContentService service,
                VocabularyCatalog vocabulary, KanjiCatalog kanji, string language = "en",
                CancellationToken cancellationToken = default) =>
            {
                if (!IsValidLanguage(language)) return InvalidQuery();
                if (request.ContentType is not ("vocabulary" or "kanji") || string.IsNullOrWhiteSpace(request.CatalogContentId))
                    return Validation(nameof(request.ContentType), "Content type and catalog content ID must identify a valid catalog entry.");
                var userId = GetUserId(principal);
                if (userId is null) return UnauthorizedIdentity();

                if (request.ContentType == "vocabulary" && vocabulary.Find(request.CatalogContentId, language) is null ||
                    request.ContentType == "kanji" && kanji.Find(request.CatalogContentId) is null)
                    return Validation(nameof(request.CatalogContentId), "The catalog ID does not exist for the requested content type.");

                try
                {
                    var (item, alreadyPresent) = await service.AddCatalogReferenceAsync(
                        userId, request.ContentType, request.CatalogContentId, cancellationToken);
                    return TypedResults.Ok(new AddKnownCatalogResponse(ToResponse(item, language, vocabulary, kanji), alreadyPresent));
                }
                catch (KnownContentLimitExceededException) { return ListLimit(); }
            })
            .WithName("AddMyKnownCatalogContent")
            .WithSummary("Add a canonical vocabulary or kanji catalog entry")
            .WithDescription("The catalog ID is resolved and validated by the server; repeated additions are idempotent.")
            .Produces<AddKnownCatalogResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/custom/vocabulary", async Task<IResult> (
                CreateKnownVocabularyRequest request, ClaimsPrincipal principal, IKnownContentService service,
                VocabularyCatalog vocabulary, KanjiCatalog kanji, CancellationToken cancellationToken) =>
            {
                var fields = ValidateVocabulary(request);
                if (fields.Count != 0) return TypedResults.ValidationProblem(fields);
                var userId = GetUserId(principal);
                if (userId is null) return UnauthorizedIdentity();
                var word = KnownContentNormalization.NormalizePresented(request.Word);
                var reading = KnownContentNormalization.NormalizePresented(request.Reading);
                var normalizedWord = KnownContentNormalization.NormalizeWriting(word);
                var normalizedReading = KnownContentNormalization.NormalizeReading(reading);
                var official = vocabulary.FindByNormalizedContent(normalizedWord, normalizedReading, "en");
                if (official is not null) return CatalogDuplicate("vocabulary", official.ContentId);
                if (await service.HasCustomAsync(userId, "vocabulary", normalizedWord, normalizedReading, cancellationToken))
                    return Duplicate();

                try
                {
                    var item = await service.AddCustomAsync(userId, new UserKnownContent
                    {
                        UserId = userId, ContentType = "vocabulary", Source = "custom",
                        Word = word, Reading = reading,
                        Romaji = KnownContentNormalization.NormalizePresented(request.Romaji),
                        Meaning = KnownContentNormalization.NormalizePresented(request.Meaning),
                        NormalizedWriting = normalizedWord, NormalizedReading = normalizedReading
                    }, cancellationToken);
                    return TypedResults.Created($"/api/v1/me/known-content/{item.Id}",
                        ToResponse(item, "en", vocabulary, kanji));
                }
                catch (KnownContentLimitExceededException) { return ListLimit(); }
                catch (KnownContentDuplicateException) { return Duplicate(); }
            })
            .WithName("CreateMyKnownVocabulary")
            .WithSummary("Create a private vocabulary item")
            .Produces<KnownContentItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/custom/kanji", async Task<IResult> (
                CreateKnownKanjiRequest request, ClaimsPrincipal principal, IKnownContentService service,
                KanjiCatalog kanji, VocabularyCatalog vocabulary, CancellationToken cancellationToken) =>
            {
                var fields = ValidateKanji(request);
                if (fields.Count != 0) return TypedResults.ValidationProblem(fields);
                var userId = GetUserId(principal);
                if (userId is null) return UnauthorizedIdentity();
                var character = KnownContentNormalization.NormalizePresented(request.Kanji);
                var normalized = KnownContentNormalization.NormalizeWriting(character);
                var official = kanji.FindByNormalizedCharacter(normalized);
                if (official is not null) return CatalogDuplicate("kanji", official.Id);
                if (await service.HasCustomAsync(userId, "kanji", normalized, null, cancellationToken))
                    return Duplicate();
                var readings = request.Readings.Select(KnownContentNormalization.NormalizePresented).ToArray();
                var meanings = request.Meanings.Select(KnownContentNormalization.NormalizePresented).ToArray();
                try
                {
                    var item = await service.AddCustomAsync(userId, new UserKnownContent
                    {
                        UserId = userId, ContentType = "kanji", Source = "custom", Kanji = character,
                        Readings = readings, Meanings = meanings, NormalizedWriting = normalized
                    }, cancellationToken);
                    return TypedResults.Created($"/api/v1/me/known-content/{item.Id}",
                        ToResponse(item, "en", vocabulary, kanji));
                }
                catch (KnownContentLimitExceededException) { return ListLimit(); }
                catch (KnownContentDuplicateException) { return Duplicate(); }
            })
            .WithName("CreateMyKnownKanji")
            .WithSummary("Create a private kanji item")
            .Produces<KnownContentItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async Task<IResult> (
                Guid id, ClaimsPrincipal principal, IKnownContentService service, CancellationToken cancellationToken) =>
            {
                var userId = GetUserId(principal);
                if (userId is not null) await service.RemoveAsync(userId, id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RemoveMyKnownContent")
            .WithSummary("Remove an entry from the authenticated user's list")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static KnownContentItemResponse ToResponse(UserKnownContent item, string language,
        VocabularyCatalog vocabulary, KanjiCatalog kanji)
    {
        object content;
        if (item.Source == "custom" && item.ContentType == "vocabulary")
            content = new KnownVocabularyContent(item.Word!, item.Reading!, item.Romaji!, item.Meaning!, null);
        else if (item.Source == "custom")
            content = new KnownKanjiContent(item.Kanji!, item.Readings!, item.Meanings!, null);
        else if (item.ContentType == "vocabulary")
        {
            var entry = vocabulary.Find(item.CatalogContentId!, language)
                ?? throw new InvalidOperationException("A stored vocabulary catalog reference is no longer resolvable.");
            content = new KnownVocabularyContent(entry.Word, entry.Furigana, entry.Romaji, entry.Meaning, entry.Level);
        }
        else
        {
            var entry = kanji.Find(item.CatalogContentId!)
                ?? throw new InvalidOperationException("A stored kanji catalog reference is no longer resolvable.");
            var meanings = language == "es" && entry.MeaningsEs.Count > 0 ? entry.MeaningsEs : entry.MeaningsEn;
            content = new KnownKanjiContent(entry.Kanji,
                entry.OnReadings.Concat(entry.KunReadings).Concat(entry.NameReadings).Distinct().ToArray(), meanings, entry.Jlpt);
        }
        return new KnownContentItemResponse(item.Id, item.ContentType, item.Source, item.CatalogContentId, item.CreatedAt, content);
    }

    private static Dictionary<string, string[]> ValidateVocabulary(CreateKnownVocabularyRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        CheckText(nameof(request.Word), request.Word, 100, errors);
        CheckText(nameof(request.Reading), request.Reading, 200, errors);
        CheckText(nameof(request.Romaji), request.Romaji, 200, errors);
        CheckText(nameof(request.Meaning), request.Meaning, 500, errors);
        if (!string.IsNullOrWhiteSpace(request.Reading) && !KnownContentNormalization.IsKanaReading(request.Reading.Trim()))
            errors[nameof(request.Reading)] = ["Reading must contain kana characters and long-vowel marks only."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateKanji(CreateKnownKanjiRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        CheckText(nameof(request.Kanji), request.Kanji, 100, errors);
        if (!string.IsNullOrWhiteSpace(request.Kanji) && !KnownContentNormalization.IsKanjiCharacter(request.Kanji.Trim()))
            errors[nameof(request.Kanji)] = ["Kanji must be exactly one assigned Han ideograph."];
        if (request.Readings is null || request.Readings.Length is < 1 or > 20)
            errors[nameof(request.Readings)] = ["Provide between 1 and 20 readings."];
        else for (var i = 0; i < request.Readings.Length; i++)
        {
            CheckText($"Readings[{i}]", request.Readings[i], 200, errors);
            if (!string.IsNullOrWhiteSpace(request.Readings[i]) && !KnownContentNormalization.IsKanaReading(request.Readings[i].Trim()))
                errors[$"Readings[{i}]"] = ["Reading must contain kana characters and long-vowel marks only."];
        }
        if (request.Meanings is null || request.Meanings.Length is < 1 or > 20)
            errors[nameof(request.Meanings)] = ["Provide between 1 and 20 meanings."];
        else for (var i = 0; i < request.Meanings.Length; i++) CheckText($"Meanings[{i}]", request.Meanings[i], 500, errors);
        return errors;
    }

    private static void CheckText(string field, string? value, int maximum, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) { errors[field] = ["Value must not be empty."]; return; }
        var trimmed = value.Trim();
        if (KnownContentNormalization.ScalarLength(trimmed) > maximum)
            errors[field] = [$"Value must contain at most {maximum} Unicode scalar values."];
        else if (trimmed.EnumerateRunes().Any(Rune.IsControl))
            errors[field] = ["Control characters are not allowed."];
    }

    private static IResult CatalogDuplicate(string type, string id) => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Catalog duplicate",
        detail: "An equivalent official catalog entry exists. Add that catalog entry instead.", extensions: new Dictionary<string, object?>
        { ["code"] = "catalog-duplicate", ["contentType"] = type, ["catalogContentId"] = id });

    private static IResult Duplicate() => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Duplicate known content", detail: "An equivalent custom item is already in your list.",
        extensions: new Dictionary<string, object?> { ["code"] = "duplicate" });

    private static IResult ListLimit() => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Known-content list limit reached", detail: "A list can contain at most 1000 entries.",
        extensions: new Dictionary<string, object?> { ["code"] = "list-limit" });

    private static IResult Validation(string field, string message) => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] });

    private static IResult InvalidQuery() => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid known-content query", detail: "Type must be vocabulary or kanji; offset must be non-negative; limit 1–200; language en or es.");

    private static IResult UnauthorizedIdentity() => TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized,
        title: "Unauthorized", detail: "The authenticated user identity is unavailable.");

    private static bool IsValidType(string? type) => type is null or "vocabulary" or "kanji";
    private static bool IsValidLanguage(string language) => language is "en" or "es";
    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
