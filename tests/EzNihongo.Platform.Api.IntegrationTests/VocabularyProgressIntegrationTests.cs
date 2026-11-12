using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EzNihongo.Platform.Api.Authentication;
using EzNihongo.Platform.Api.Identity;
using EzNihongo.Platform.Api.Vocabulary;
using EzNihongo.Platform.Api.VocabularyProgress;
using EzNihongo.Platform.Api.KnownContent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EzNihongo.Platform.Api.IntegrationTests;

public sealed class VocabularyProgressIntegrationTests : IAsyncLifetime
{
    private const string JwtSigningKey = "integration-test-signing-key-with-more-than-32-bytes";
    private readonly string _connectionString = Environment.GetEnvironmentVariable("AUTH_TEST_POSTGRES_CONNECTION")!;
    private readonly TestApiFactory _factory;
    private HttpClient _client = null!;

    public VocabularyProgressIntegrationTests() => _factory = new TestApiFactory(_connectionString);

    public async Task InitializeAsync()
    {
        _client = TestHost.CreateClient(_factory.CreateClient, authPermitLimit: 100);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();
        await database.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [PostgresFact]
    public async Task Attempts_require_authentication_are_idempotent_and_are_private_per_user()
    {
        const string attemptsEndpoint = "/api/v1/progress/vocabulary/attempts";
        var anonymousGet = await _client.GetAsync(attemptsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousGet.StatusCode);
        var anonymousPost = await _client.PostAsJsonAsync(attemptsEndpoint, new
        {
            eventId = "anonymous-event",
            sessionId = "session",
            contentId = "unused",
            exerciseType = "reading",
            correct = true
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        using var firstUser = await CreateAuthenticatedClient();
        using var secondUser = await CreateAuthenticatedClient();
        var catalog = await firstUser.GetFromJsonAsync<VocabularyPage>("/api/v1/vocabulary?level=5&limit=1");
        Assert.NotNull(catalog);
        var contentId = Assert.Single(catalog.Words).ContentId;
        var request = new
        {
            eventId = $"event-{Guid.NewGuid():N}",
            sessionId = $"session-{Guid.NewGuid():N}",
            contentId,
            exerciseType = "reading",
            correct = false
        };

        var created = await firstUser.PostAsJsonAsync(attemptsEndpoint, request);
        var duplicate = await firstUser.PostAsJsonAsync(attemptsEndpoint, request);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicate.StatusCode);

        var firstPage = await firstUser.GetFromJsonAsync<VocabularyAttemptPageResponse>($"{attemptsEndpoint}?limit=1");
        var secondPage = await secondUser.GetFromJsonAsync<VocabularyAttemptPageResponse>(attemptsEndpoint);
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(1, firstPage.Total);
        var savedAttempt = Assert.Single(firstPage.Attempts);
        Assert.Equal(request.eventId, savedAttempt.EventId);
        Assert.Equal(contentId, savedAttempt.ContentId);
        Assert.Equal("reading", savedAttempt.ExerciseType);
        Assert.False(savedAttempt.Correct);
        Assert.True(DateTimeOffset.UtcNow - savedAttempt.CreatedAt < TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.Zero, savedAttempt.CreatedAt.Offset);
        Assert.Equal(0, secondPage.Total);

        var invalidPage = await firstUser.GetAsync($"{attemptsEndpoint}?limit=201");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
    }

    [PostgresFact]
    public async Task Known_content_routes_validate_auth_catalog_custom_duplicates_paging_and_delete()
    {
        const string endpoint = "/api/v1/me/known-content";
        var anonymous = await _client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var anonymousPost = await _client.PostAsJsonAsync($"{endpoint}/catalog", new
        {
            contentType = "vocabulary", catalogContentId = "unused"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        using var firstUser = await CreateAuthenticatedClient();
        using var secondUser = await CreateAuthenticatedClient();
        var invalidCatalog = await firstUser.PostAsJsonAsync($"{endpoint}/catalog", new
        {
            contentType = "kanji", catalogContentId = "not-a-kanji-id"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCatalog.StatusCode);

        var catalogPage = await firstUser.GetFromJsonAsync<VocabularyPage>("/api/v1/vocabulary?level=5&limit=1");
        Assert.NotNull(catalogPage);
        var catalogEntry = Assert.Single(catalogPage.Words);
        var catalogId = catalogEntry.ContentId;
        var catalogDuplicate = await firstUser.PostAsJsonAsync($"{endpoint}/custom/vocabulary", new
        {
            word = catalogEntry.Word,
            reading = catalogEntry.Furigana,
            romaji = catalogEntry.Romaji,
            meaning = catalogEntry.Meaning
        });
        Assert.Equal(HttpStatusCode.Conflict, catalogDuplicate.StatusCode);
        var catalogDuplicateBody = await catalogDuplicate.Content.ReadAsStringAsync();
        Assert.Contains("catalog-duplicate", catalogDuplicateBody, StringComparison.Ordinal);
        Assert.Contains(catalogId, catalogDuplicateBody, StringComparison.Ordinal);

        var catalogRequest = new { contentType = "vocabulary", catalogContentId = catalogId };
        var addedCatalog = await firstUser.PostAsJsonAsync($"{endpoint}/catalog", catalogRequest);
        var repeatedCatalog = await firstUser.PostAsJsonAsync($"{endpoint}/catalog", catalogRequest);
        Assert.Equal(HttpStatusCode.OK, addedCatalog.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedCatalog.StatusCode);
        Assert.False((await addedCatalog.Content.ReadFromJsonAsync<AddKnownCatalogResponse>())!.AlreadyPresent);
        Assert.True((await repeatedCatalog.Content.ReadFromJsonAsync<AddKnownCatalogResponse>())!.AlreadyPresent);

        var invalidCustom = await firstUser.PostAsJsonAsync($"{endpoint}/custom/vocabulary", new
        {
            word = "自作", reading = "kana", romaji = "test", meaning = "invalid reading"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCustom.StatusCode);

        var customWord = $"自作{Guid.NewGuid():N}";
        var customRequest = new { word = customWord, reading = "てすと", romaji = "test", meaning = "private" };
        var created = await firstUser.PostAsJsonAsync($"{endpoint}/custom/vocabulary", customRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var custom = await created.Content.ReadFromJsonAsync<KnownContentItemResponse>();
        Assert.NotNull(custom);
        Assert.Equal("custom", custom.Source);
        Assert.Null(custom.CatalogContentId);
        var duplicate = await firstUser.PostAsJsonAsync($"{endpoint}/custom/vocabulary", customRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("duplicate", duplicateBody, StringComparison.Ordinal);

        var customKanjiResponse = await firstUser.PostAsJsonAsync($"{endpoint}/custom/kanji", new
        {
            kanji = "𠀀",
            readings = new[] { "しかる" },
            meanings = new[] { "scold" }
        });
        Assert.Equal(HttpStatusCode.Created, customKanjiResponse.StatusCode);
        var customKanji = await customKanjiResponse.Content.ReadFromJsonAsync<KnownContentItemResponse>();
        Assert.NotNull(customKanji);
        Assert.Equal("kanji", customKanji.ContentType);
        Assert.Equal("custom", customKanji.Source);
        Assert.Null(customKanji.CatalogContentId);

        var page = await firstUser.GetFromJsonAsync<KnownContentPageResponse>($"{endpoint}?type=vocabulary&offset=0&limit=1");
        Assert.NotNull(page);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        var exerciseSet = await firstUser.GetFromJsonAsync<KnownContentPageResponse>($"{endpoint}?offset=0&limit=200");
        Assert.NotNull(exerciseSet);
        Assert.Equal(3, exerciseSet.Total);
        Assert.Equal(3, exerciseSet.Items.Count);
        Assert.Contains(exerciseSet.Items, item => item.Source == "catalog" && item.CatalogContentId == catalogId);
        Assert.Contains(exerciseSet.Items, item => item.Id == custom.Id && item.ContentType == "vocabulary");
        Assert.Contains(exerciseSet.Items, item => item.Id == customKanji.Id && item.ContentType == "kanji");
        var kanjiSet = await firstUser.GetFromJsonAsync<KnownContentPageResponse>($"{endpoint}?type=kanji&offset=0&limit=200");
        Assert.NotNull(kanjiSet);
        Assert.Single(kanjiSet.Items);
        Assert.Equal(customKanji.Id, kanjiSet.Items[0].Id);
        Assert.Equal(HttpStatusCode.OK, (await firstUser.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await firstUser.GetAsync($"{endpoint}?type=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await firstUser.GetAsync($"{endpoint}?language=fr")).StatusCode);
        var otherPage = await secondUser.GetFromJsonAsync<KnownContentPageResponse>(endpoint);
        Assert.NotNull(otherPage);
        Assert.Equal(0, otherPage.Total);

        var otherUserRemoval = await secondUser.DeleteAsync($"{endpoint}/{custom.Id}");
        Assert.Equal(HttpStatusCode.NoContent, otherUserRemoval.StatusCode);
        var stillPresent = await firstUser.GetFromJsonAsync<KnownContentPageResponse>(endpoint);
        Assert.NotNull(stillPresent);
        Assert.Contains(stillPresent.Items, item => item.Id == custom.Id);

        var removed = await firstUser.DeleteAsync($"{endpoint}/{custom.Id}");
        var repeatedRemoval = await firstUser.DeleteAsync($"{endpoint}/{custom.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedRemoval.StatusCode);
        var invalidLimit = await firstUser.GetAsync($"{endpoint}?limit=201");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Correct-Horse7!"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var session = await register.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private sealed class TestApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Postgres:ConnectionString"] = connectionString,
                    ["Jwt:SigningKey"] = JwtSigningKey,
                    ["Jwt:Issuer"] = "ez-nihongo-integration-tests",
                    ["Jwt:Audience"] = "ez-nihongo-integration-tests",
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["AuthRateLimit:PermitLimit"] = "100",
                    ["AuthRateLimit:WindowMinutes"] = "1",
                    ["PersistentLogin:AllowedOrigins:0"] = "http://localhost",
                    ["Vocabulary:DataDirectory"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db")),
                    ["Kanji:DataDirectory"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/kanji"))
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PlatformIdentityDbContext>>();
                services.AddDbContext<PlatformIdentityDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }
}
