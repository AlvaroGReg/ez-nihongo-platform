using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EzNihongo.Platform.Api.Authentication;
using EzNihongo.Platform.Api.Identity;
using EzNihongo.Platform.Api.Vocabulary;
using EzNihongo.Platform.Api.VocabularyProgress;
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
