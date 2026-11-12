using Microsoft.EntityFrameworkCore;
using EzNihongo.Platform.Api.Identity;

namespace EzNihongo.Platform.Api.KnownContent;

public interface IKnownContentService
{
    Task<(int Total, IReadOnlyList<UserKnownContent> Items)> GetPageAsync(
        string userId, string? contentType, int offset, int limit, CancellationToken cancellationToken);
    Task<(UserKnownContent Item, bool AlreadyPresent)> AddCatalogReferenceAsync(
        string userId, string contentType, string catalogContentId, CancellationToken cancellationToken);
    Task<UserKnownContent> AddCustomAsync(string userId, UserKnownContent item, CancellationToken cancellationToken);
    Task<bool> HasCustomAsync(string userId, string contentType, string normalizedWriting, string? normalizedReading, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string userId, Guid id, CancellationToken cancellationToken);
}

public sealed class KnownContentService(PlatformIdentityDbContext database) : IKnownContentService
{
    private const int MaximumItemsPerUser = 1000;

    public async Task<(int Total, IReadOnlyList<UserKnownContent> Items)> GetPageAsync(
        string userId, string? contentType, int offset, int limit, CancellationToken cancellationToken)
    {
        var query = database.UserKnownContent.AsNoTracking().Where(item => item.UserId == userId);
        if (contentType is not null)
            query = query.Where(item => item.ContentType == contentType);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        return (total, items);
    }

    public async Task<(UserKnownContent Item, bool AlreadyPresent)> AddCatalogReferenceAsync(
        string userId, string contentType, string catalogContentId, CancellationToken cancellationToken)
    {
        var existing = await database.UserKnownContent.SingleOrDefaultAsync(item =>
            item.UserId == userId && item.ContentType == contentType && item.Source == "catalog" &&
            item.CatalogContentId == catalogContentId, cancellationToken);
        if (existing is not null)
            return (existing, true);

        await EnsureCapacityAsync(userId, cancellationToken);

        var item = new UserKnownContent
        {
            Id = Guid.NewGuid(), UserId = userId, ContentType = contentType, Source = "catalog",
            CatalogContentId = catalogContentId, CreatedAt = DateTimeOffset.UtcNow
        };
        database.UserKnownContent.Add(item);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return (item, false);
        }
        catch (DbUpdateException)
        {
            database.Entry(item).State = EntityState.Detached;
            var concurrent = await database.UserKnownContent.SingleOrDefaultAsync(value =>
                value.UserId == userId && value.ContentType == contentType && value.Source == "catalog" &&
                value.CatalogContentId == catalogContentId, cancellationToken);
            if (concurrent is not null)
                return (concurrent, true);

            throw;
        }
    }

    public async Task<UserKnownContent> AddCustomAsync(
        string userId, UserKnownContent item, CancellationToken cancellationToken)
    {
        await EnsureCapacityAsync(userId, cancellationToken);
        item.Id = Guid.NewGuid();
        item.UserId = userId;
        item.Source = "custom";
        item.CatalogContentId = null;
        item.CreatedAt = DateTimeOffset.UtcNow;
        database.UserKnownContent.Add(item);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return item;
        }
        catch (DbUpdateException)
        {
            database.Entry(item).State = EntityState.Detached;
            if (await HasCustomAsync(userId, item.ContentType, item.NormalizedWriting!, item.NormalizedReading, cancellationToken))
                throw new KnownContentDuplicateException();
            throw;
        }
    }

    public Task<bool> HasCustomAsync(
        string userId, string contentType, string normalizedWriting, string? normalizedReading,
        CancellationToken cancellationToken) => database.UserKnownContent.AnyAsync(item =>
            item.UserId == userId && item.ContentType == contentType && item.Source == "custom" &&
            item.NormalizedWriting == normalizedWriting && item.NormalizedReading == normalizedReading,
            cancellationToken);

    private async Task EnsureCapacityAsync(string userId, CancellationToken cancellationToken)
    {
        if (await database.UserKnownContent.CountAsync(item => item.UserId == userId, cancellationToken)
            >= MaximumItemsPerUser)
            throw new KnownContentLimitExceededException();
    }

    public async Task<bool> RemoveAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var item = await database.UserKnownContent.SingleOrDefaultAsync(
            value => value.Id == id && value.UserId == userId, cancellationToken);
        if (item is null)
            return false;

        database.UserKnownContent.Remove(item);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class KnownContentLimitExceededException()
    : InvalidOperationException("The known-content list has reached its maximum size.");

public sealed class KnownContentDuplicateException()
    : InvalidOperationException("The custom item is already in the known-content list.");
