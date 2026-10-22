using System.ComponentModel.DataAnnotations;

namespace EzNihongo.Platform.Api.VocabularyProgress;

/// <summary>Payload describing a vocabulary exercise attempt.</summary>
public sealed record CreateVocabularyAttemptRequest
{
    [Required, StringLength(100)]
    public required string EventId { get; init; }

    [Required, StringLength(100)]
    public required string SessionId { get; init; }

    [Required, StringLength(300)]
    public required string ContentId { get; init; }

    [Required, StringLength(32)]
    public required string ExerciseType { get; init; }

    public required bool Correct { get; init; }
}

/// <summary>A vocabulary attempt recorded for the authenticated user.</summary>
public sealed record VocabularyAttemptResponse(
    string EventId,
    string SessionId,
    string ContentId,
    string ExerciseType,
    bool Correct,
    DateTimeOffset CreatedAt);

/// <summary>A page of vocabulary attempts owned by the authenticated user.</summary>
public sealed record VocabularyAttemptPageResponse(
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<VocabularyAttemptResponse> Attempts);
