using System.Text;
using EzNihongo.Platform.Api.KnownContent;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EzNihongo.Platform.Api.IntegrationTests;

public sealed class KnownContentPersistenceTests
{
    [Theory]
    [InlineData("  ｶﾞｸ  ", "ｶﾞｸ", "がく")]
    [InlineData("ｶﾞｸー", "ｶﾞｸー", "がくー")]
    [InlineData("まなぶ", "まなぶ", "まなぶ")]
    public void Reading_key_uses_trim_nfkc_and_katakana_to_hiragana(string input, string presented, string key)
    {
        Assert.Equal(presented.Normalize(), KnownContentNormalization.NormalizePresented(input));
        Assert.Equal(key, KnownContentNormalization.NormalizeReading(input));
    }

    [Fact]
    public void Scalar_count_and_reading_validation_follow_unicode_scalars()
    {
        Assert.Equal(1, KnownContentNormalization.ScalarLength("𠮟"));
        Assert.True(KnownContentNormalization.IsKanjiCharacter("𠮟"));
        Assert.False(KnownContentNormalization.IsKanjiCharacter("学々"));
        Assert.True(KnownContentNormalization.IsKanaReading("がっこうー"));
        Assert.False(KnownContentNormalization.IsKanaReading("かな・"));
    }

    [PostgresFact]
    public async Task Migration_upgrades_existing_identity_schema_and_unique_indexes_protect_keys()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_TEST_POSTGRES_CONNECTION")!;
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext>()
            .UseNpgsql(connection).Options;
        var userId = $"known-content-{Guid.NewGuid():N}";
        await using var database = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(options);
        await database.Database.MigrateAsync("20261022124733_PersistentLoginSessions");

        database.Users.Add(new EzNihongo.Platform.Api.Identity.PlatformUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST"
        });
        await database.SaveChangesAsync();

        try
        {
            await database.Database.MigrateAsync();
            await using (var verification = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(options))
                Assert.NotNull(await verification.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId));

            database.UserKnownContent.AddRange(
                CatalogItem(userId, "vocabulary", "vocabulary:1"),
                CustomVocabulary(userId, "勉強", "がく"));
            await database.SaveChangesAsync();

            database.UserKnownContent.Add(CatalogItem(userId, "vocabulary", "vocabulary:1"));
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => database.SaveChangesAsync());
            database.ChangeTracker.Clear();

            database.UserKnownContent.Add(CustomVocabulary(userId, "勉強", "ガク"));
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => database.SaveChangesAsync());
        }
        finally
        {
            database.ChangeTracker.Clear();
            var user = await database.Users.FindAsync(userId);
            if (user is not null)
            {
                database.Users.Remove(user);
                await database.SaveChangesAsync();
            }
        }
    }

    [PostgresFact]
    public async Task Concurrent_catalog_membership_writes_create_one_row_and_return_same_item()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_TEST_POSTGRES_CONNECTION")!;
        var userId = $"known-content-race-{Guid.NewGuid():N}";
        var userOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext>()
            .UseNpgsql(connection).Options;

        await using (var setup = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(userOptions))
        {
            await setup.Database.MigrateAsync();
            setup.Users.Add(new EzNihongo.Platform.Api.Identity.PlatformUser
            {
                Id = userId,
                UserName = $"{userId}@example.test",
                NormalizedUserName = $"{userId}@EXAMPLE.TEST",
                Email = $"{userId}@example.test",
                NormalizedEmail = $"{userId}@EXAMPLE.TEST"
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            var writes = Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var context = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(userOptions);
                var service = new KnownContentService(context);
                return await service.AddCatalogReferenceAsync(
                    userId, "vocabulary", "vocabulary:concurrent", CancellationToken.None);
            });

            var results = await Task.WhenAll(writes);
            Assert.Equal(results[0].Item.Id, results[1].Item.Id);
            Assert.Equal(1, results.Count(result => result.AlreadyPresent));

            await using var verification = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(userOptions);
            Assert.Equal(1, await verification.UserKnownContent.CountAsync(item =>
                item.UserId == userId && item.ContentType == "vocabulary" &&
                item.CatalogContentId == "vocabulary:concurrent"));
        }
        finally
        {
            await using var cleanup = new EzNihongo.Platform.Api.Identity.PlatformIdentityDbContext(userOptions);
            var user = await cleanup.Users.FindAsync(userId);
            if (user is not null)
            {
                cleanup.Users.Remove(user);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    private static UserKnownContent CatalogItem(string userId, string type, string catalogId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, ContentType = type, Source = "catalog",
        CatalogContentId = catalogId, CreatedAt = DateTimeOffset.UtcNow
    };

    private static UserKnownContent CustomVocabulary(string userId, string word, string reading) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, ContentType = "vocabulary", Source = "custom",
        Word = word, Reading = reading, Romaji = "gaku", Meaning = "study",
        NormalizedWriting = KnownContentNormalization.NormalizeWriting(word),
        NormalizedReading = KnownContentNormalization.NormalizeReading(reading),
        CreatedAt = DateTimeOffset.UtcNow
    };
}
