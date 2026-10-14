using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EzNihongo.Platform.Api.Authentication;
using EzNihongo.Platform.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EzNihongo.Platform.Api.IntegrationTests;

public sealed class AuthIntegrationTests : IAsyncLifetime
{
    private const string JwtSigningKey = "integration-test-signing-key-with-more-than-32-bytes";
    private readonly string _connectionString;
    private readonly TestApiFactory _factory;
    private HttpClient _client = null!;

    public AuthIntegrationTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("AUTH_TEST_POSTGRES_CONNECTION")!;
        _factory = new TestApiFactory(_connectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
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
    public async Task Register_persists_normalized_account_and_returns_usable_token()
    {
        var email = NewEmail();
        const string password = "Correct-Horse7!";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/auth/me", response.Headers.Location?.OriginalString);
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(token);
        Assert.Equal(email, token.User.Email);
        Assert.Equal("Bearer", token.TokenType);
        Assert.DoesNotContain(password, await response.Content.ReadAsStringAsync());

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var identity = await _client.GetFromJsonAsync<AuthIdentityResponse>("/api/v1/auth/me");
        Assert.NotNull(identity);
        Assert.Equal(token.User.Id, identity.Id);
        Assert.Equal(email, identity.Email);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash!, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
    }

    [PostgresFact]
    public async Task Duplicate_email_is_conflict_even_when_case_differs()
    {
        var email = NewEmail();
        await Register(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = email.ToUpperInvariant(),
            password = "Another-Valid9!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Concurrent_registration_of_same_email_creates_only_one_account()
    {
        var email = NewEmail();
        var attempts = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Correct-Horse7!" }),
            _client.PostAsJsonAsync("/api/v1/auth/register", new { email = email.ToUpperInvariant(), password = "Correct-Horse7!" }));

        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [PostgresFact]
    public async Task Login_returns_same_generic_unauthorized_for_unknown_email_and_wrong_password()
    {
        var email = NewEmail();
        await Register(email);

        var existing = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong-Password9!" });
        var missing = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = NewEmail(), password = "Wrong-Password9!" });

        Assert.Equal(HttpStatusCode.Unauthorized, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Wrong-Password9!", await existing.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Login_and_me_require_valid_access_token()
    {
        var email = NewEmail();
        await Register(email);
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Correct-Horse7!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(token);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(email, (await _client.GetFromJsonAsync<AuthIdentityResponse>("/api/v1/auth/me"))?.Email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken + "tampered");
        var invalidTokenResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidTokenResponse.StatusCode);
        Assert.Equal("application/problem+json", invalidTokenResponse.Content.Headers.ContentType?.MediaType);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateExpiredToken());
        var expiredTokenResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredTokenResponse.StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
        var missingTokenResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missingTokenResponse.StatusCode);
    }

    [PostgresFact]
    public async Task Public_health_and_vocabulary_endpoints_remain_anonymous()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/vocabulary/levels")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/vocabulary?level=5&limit=1")).StatusCode);
    }

    [PostgresFact]
    public async Task Invalid_registration_and_excess_requests_return_problem_details()
    {
        var invalid = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = "not-an-email", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("short", await invalid.Content.ReadAsStringAsync());

        using var limiterClient = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? limitedResponse = null;
        for (var index = 0; index < 4; index++)
        {
            var response = await limiterClient.PostAsJsonAsync("/api/v1/auth/login", new { email = NewEmail(), password = "Wrong-Password9!" });
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                limitedResponse = response;
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.NotNull(limitedResponse);
        Assert.Equal("application/problem+json", limitedResponse.Content.Headers.ContentType?.MediaType);
    }

    private async Task Register(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Correct-Horse7!" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private static string CreateExpiredToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: "ez-nihongo-integration-tests",
            audience: "ez-nihongo-integration-tests",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "expired-test-user")],
            notBefore: DateTime.UtcNow.AddMinutes(-2),
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
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
                    ["AuthRateLimit:PermitLimit"] = "3",
                    ["AuthRateLimit:WindowMinutes"] = "1",
                    ["Vocabulary:DataDirectory"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db"))
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PlatformIdentityDbContext>>();
                services.AddDbContext<PlatformIdentityDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }
}

internal sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTH_TEST_POSTGRES_CONNECTION")))
            Skip = "Set AUTH_TEST_POSTGRES_CONNECTION to a disposable PostgreSQL database to run integration tests.";
    }
}
