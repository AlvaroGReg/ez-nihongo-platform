using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EzNihongo.Platform.Api.KnownContent;

/// <summary>Request to add a canonical catalog entry to the authenticated user's list.</summary>
public sealed record AddKnownCatalogRequest
{
    [Required]
    public required string ContentType { get; init; }

    [Required]
    public required string CatalogContentId { get; init; }
}

/// <summary>Request to create a private vocabulary entry.</summary>
public sealed record CreateKnownVocabularyRequest
{
    [Required] public required string Word { get; init; }
    [Required] public required string Reading { get; init; }
    [Required] public required string Romaji { get; init; }
    [Required] public required string Meaning { get; init; }
}

/// <summary>Request to create a private kanji entry.</summary>
public sealed record CreateKnownKanjiRequest
{
    [Required] public required string Kanji { get; init; }
    [Required, MinLength(1), MaxLength(20)] public required string[] Readings { get; init; }
    [Required, MinLength(1), MaxLength(20)] public required string[] Meanings { get; init; }
}

/// <summary>Content fields for a vocabulary item returned by the known-content API.</summary>
public sealed record KnownVocabularyContent(
    string Word, string Reading, string Romaji, string Meaning,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Level);

/// <summary>Content fields for a kanji item returned by the known-content API.</summary>
public sealed record KnownKanjiContent(
    string Kanji, IReadOnlyList<string> Readings, IReadOnlyList<string> Meanings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Level);

/// <summary>A canonical catalog item or private item in a user's known-content list.</summary>
public sealed record KnownContentItemResponse(
    Guid Id, string ContentType, string Source, string? CatalogContentId,
    DateTimeOffset CreatedAt, object Content);

/// <summary>A page of the authenticated user's known-content list.</summary>
public sealed record KnownContentPageResponse(int Total, int Offset, int Limit, IReadOnlyList<KnownContentItemResponse> Items);

/// <summary>Result of adding a catalog item, including whether it was already present.</summary>
public sealed record AddKnownCatalogResponse(KnownContentItemResponse Item, bool AlreadyPresent);
