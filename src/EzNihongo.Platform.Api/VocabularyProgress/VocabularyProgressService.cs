using EzNihongo.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.VocabularyProgress;

public sealed class VocabularyProgressService(
    PlatformIdentityDbContext database,
    TimeProvider timeProvider) : IVocabularyProgressService
{
    public async Task<bool> RecordAttemptAsync(
        string userId,
        CreateVocabularyAttemptRequest request,
        CancellationToken cancellationToken)
    {
        var exists = await database.VocabularyAttempts.AnyAsync(
            attempt => attempt.UserId == userId && attempt.EventId == request.EventId,
            cancellationToken);
        if (exists) return false;

        var attempt = new VocabularyAttempt
        {
            UserId = userId,
            EventId = request.EventId,
            SessionId = request.SessionId,
            ContentId = request.ContentId,
            ExerciseType = request.ExerciseType,
            Correct = request.Correct,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        database.VocabularyAttempts.Add(attempt);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            database.Entry(attempt).State = EntityState.Detached;
            if (await database.VocabularyAttempts.AnyAsync(
                    item => item.UserId == userId && item.EventId == request.EventId,
                    cancellationToken))
                return false;

            throw;
        }
    }

    public async Task<VocabularyAttemptPageResponse> GetAttemptsAsync(
        string userId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var userAttempts = database.VocabularyAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId);
        var total = await userAttempts.CountAsync(cancellationToken);
        var attempts = await userAttempts
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenByDescending(attempt => attempt.Id)
            .Skip(offset)
            .Take(limit)
            .Select(attempt => new VocabularyAttemptResponse(
                attempt.EventId,
                attempt.SessionId,
                attempt.ContentId,
                attempt.ExerciseType,
                attempt.Correct,
                attempt.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new VocabularyAttemptPageResponse(total, offset, limit, attempts);
    }
}
