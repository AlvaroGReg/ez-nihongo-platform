namespace EzNihongo.Platform.Api.VocabularyProgress;

public interface IVocabularyProgressService
{
    Task<bool> RecordAttemptAsync(
        string userId,
        CreateVocabularyAttemptRequest request,
        CancellationToken cancellationToken);

    Task<VocabularyAttemptPageResponse> GetAttemptsAsync(
        string userId,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}
