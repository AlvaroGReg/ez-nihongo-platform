using System.Security.Cryptography;
using EzNihongo.Platform.Api.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.Authentication;

public sealed class PersistentLoginSessionService(
    PlatformIdentityDbContext database,
    TimeProvider timeProvider) : IPersistentLoginSessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public async Task<PersistentLoginSessionResult> CreateAsync(string userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = CreateToken();
        var session = new PersistentLoginSession
        {
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(SessionLifetime)
        };
        database.PersistentLoginSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return new(PersistentLoginSessionStatus.Success, userId, token, session.ExpiresAt);
    }

    public async Task<PersistentLoginSessionResult> RotateAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryHash(token, out var hash))
            return new(PersistentLoginSessionStatus.Invalid);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var session = await database.PersistentLoginSessions
            .FromSqlInterpolated($"SELECT * FROM \"PersistentLoginSessions\" WHERE \"TokenHash\" = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PersistentLoginSessionStatus.Invalid);
        }

        var now = timeProvider.GetUtcNow();
        if (session.RotatedAt is not null)
        {
            await database.PersistentLoginSessions
                .Where(item => item.FamilyId == session.FamilyId && item.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PersistentLoginSessionStatus.Reused);
        }

        if (session.RevokedAt is not null || session.ExpiresAt <= now ||
            !await database.Users.AnyAsync(user => user.Id == session.UserId, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PersistentLoginSessionStatus.Invalid);
        }

        var replacementToken = CreateToken();
        var replacement = new PersistentLoginSession
        {
            UserId = session.UserId,
            FamilyId = session.FamilyId,
            TokenHash = Hash(replacementToken),
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt
        };
        database.PersistentLoginSessions.Add(replacement);
        session.RotatedAt = now;
        session.ReplacedById = replacement.Id;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(PersistentLoginSessionStatus.Success, session.UserId, replacementToken, session.ExpiresAt);
    }

    public async Task RevokeAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryHash(token, out var hash))
            return;
        var familyId = await database.PersistentLoginSessions
            .Where(session => session.TokenHash == hash)
            .Select(session => (Guid?)session.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (familyId is null)
            return;

        var now = timeProvider.GetUtcNow();
        await database.PersistentLoginSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    private static string CreateToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static byte[] Hash(string token) => SHA256.HashData(WebEncoders.Base64UrlDecode(token));

    private static bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrEmpty(token))
            return false;
        try
        {
            hash = Hash(token);
            return hash.Length == 32;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return false;
        }
    }
}
