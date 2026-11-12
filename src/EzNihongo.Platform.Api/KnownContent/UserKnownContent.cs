namespace EzNihongo.Platform.Api.KnownContent;

/// <summary>A catalog membership or private custom entry in one user's known-content list.</summary>
public sealed class UserKnownContent
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string ContentType { get; set; }
    public required string Source { get; set; }
    public string? CatalogContentId { get; set; }
    public string? Word { get; set; }
    public string? Reading { get; set; }
    public string? Romaji { get; set; }
    public string? Meaning { get; set; }
    public string? Kanji { get; set; }
    public string[]? Readings { get; set; }
    public string[]? Meanings { get; set; }
    public string? NormalizedWriting { get; set; }
    public string? NormalizedReading { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
