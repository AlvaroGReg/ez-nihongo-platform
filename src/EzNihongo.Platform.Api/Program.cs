using System.Threading.RateLimiting;
using EzNihongo.Platform.Api.Authentication;
using EzNihongo.Platform.Api.Kanji;
using EzNihongo.Platform.Api.Vocabulary;
using EzNihongo.Platform.Api.VocabularyProgress;
using EzNihongo.Platform.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components!.SecuritySchemes![JwtBearerDefaults.AuthenticationScheme] =
            new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token"
            };
        return Task.CompletedTask;
    })
    .AddOperationTransformer((operation, context, _) =>
    {
        var path = context.Description.RelativePath?.TrimEnd('/');
        if (path == "api/v1/auth/me" || path?.StartsWith("api/v1/progress/vocabulary", StringComparison.Ordinal) == true)
        {
            operation.Security =
            [
                new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                        JwtBearerDefaults.AuthenticationScheme, context.Document)] = []
                }
            ];
        }

        return Task.CompletedTask;
    }));
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSingleton<VocabularyCatalog>();
builder.Services.AddSingleton<KanjiCatalog>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => options.IsValid(),
        $"'{JwtOptions.SectionName}' requires a signing key of at least 32 UTF-8 bytes, issuer, audience, and a positive token lifetime.")
    .ValidateOnStart();
builder.Services.AddOptions<AuthRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(AuthRateLimitOptions.SectionName))
    .Validate(options => options.IsValid(),
        $"'{AuthRateLimitOptions.SectionName}' requires positive PermitLimit and WindowMinutes values.")
    .ValidateOnStart();
builder.Services.AddOptions<PersistentLoginOptions>()
    .Bind(builder.Configuration.GetSection(PersistentLoginOptions.SectionName))
    .Validate(options => options.IsValid(builder.Environment.EnvironmentName),
        $"'{PersistentLoginOptions.SectionName}:AllowedOrigins' must contain exact origins, and insecure cookies may only be enabled in Development.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "A valid bearer access token is required.")
                    .ExecuteAsync(context.HttpContext);
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwt.IsValid() ? new SymmetricSecurityKey(jwt.GetSigningKeyBytes()) : null,
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPersistentLoginSessionService, PersistentLoginSessionService>();
builder.Services.AddScoped<IVocabularyProgressService, VocabularyProgressService>();

var rateLimit = builder.Configuration.GetSection(AuthRateLimitOptions.SectionName).Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: "Please wait before trying again.")
            .ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("auth", context =>
    {
        var partition = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimit.PermitLimit,
            Window = TimeSpan.FromMinutes(rateLimit.WindowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var postgres = builder.Configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>();
if (string.IsNullOrWhiteSpace(postgres?.ConnectionString))
{
    throw new InvalidOperationException(
        $"Required configuration '{PostgresOptions.SectionName}:ConnectionString' is missing.");
}

builder.Services.AddDbContext<PlatformIdentityDbContext>(options =>
    options.UseNpgsql(postgres.ConnectionString));
builder.Services.AddIdentityCore<PlatformUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<PlatformIdentityDbContext>();
builder.Services.AddHostedService<IdentityDatabaseStartupCheck>();

var app = builder.Build();
_ = app.Services.GetRequiredService<VocabularyCatalog>();
_ = app.Services.GetRequiredService<KanjiCatalog>();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .WithTags("Health");

var vocabulary = app.MapGroup("/api/v1/vocabulary").WithTags("Vocabulary");

app.MapAuthEndpoints();
app.MapKanjiEndpoints();
app.MapVocabularyProgressEndpoints();

vocabulary.MapGet("/levels", (VocabularyCatalog catalog) =>
        TypedResults.Ok(catalog.GetLevelCounts()))
    .WithName("GetVocabularyLevels");

vocabulary.MapGet("/", (VocabularyCatalog catalog, int? level = null, int offset = 0, int limit = 50, string language = "en") =>
    {
        if (level is < 1 or > 5)
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid JLPT level",
                detail: "The level query parameter must be between 1 and 5.");
        }

        if (offset < 0 || limit is < 1 or > 200)
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid pagination",
                detail: "Offset must be zero or greater and limit must be between 1 and 200.");
        }

        if (language is not ("en" or "es"))
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid language",
                detail: "The language query parameter must be 'en' or 'es'.");
        }

        return (IResult)TypedResults.Ok(catalog.GetPage(level, offset, limit, language));
    })
    .WithName("GetVocabulary");

vocabulary.MapGet("/{contentId}", (string contentId, VocabularyCatalog catalog, string language = "en") =>
    {
        if (language is not ("en" or "es"))
        {
            return (IResult)TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid language",
                detail: "The language query parameter must be 'en' or 'es'.");
        }

        var entry = catalog.Find(contentId, language);
        return entry is null
            ? (IResult)TypedResults.NotFound()
            : TypedResults.Ok(entry);
    })
    .WithName("GetVocabularyEntry");

app.Run();

public partial class Program;
