namespace EzNihongo.Platform.Api.VocabularyProgress;

public sealed class VocabularyAttempt
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string EventId { get; set; }
    public required string SessionId { get; set; }
    public required string ContentId { get; set; }
    public required string ExerciseType { get; set; }
    public bool Correct { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
