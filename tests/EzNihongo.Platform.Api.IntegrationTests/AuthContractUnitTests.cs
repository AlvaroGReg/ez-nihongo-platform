using System.ComponentModel.DataAnnotations;
using EzNihongo.Platform.Api.Authentication;
using Xunit;

namespace EzNihongo.Platform.Api.IntegrationTests;

public sealed class AuthContractUnitTests
{
    [Fact]
    public void Jwt_options_reject_missing_or_weak_configuration()
    {
        Assert.False(new JwtOptions().IsValid());
        Assert.False(new JwtOptions
        {
            SigningKey = "too-short",
            Issuer = "issuer",
            Audience = "audience",
            AccessTokenMinutes = 15
        }.IsValid());
    }

    [Fact]
    public void Jwt_options_accept_valid_configuration_and_positive_lifetime()
    {
        var options = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-that-is-at-least-32-bytes",
            Issuer = "issuer",
            Audience = "audience",
            AccessTokenMinutes = 15
        };

        Assert.True(options.IsValid());
        Assert.False(new JwtOptions
        {
            SigningKey = options.SigningKey,
            Issuer = options.Issuer,
            Audience = options.Audience,
            AccessTokenMinutes = 0
        }.IsValid());
    }

    [Fact]
    public void Auth_request_requires_valid_email_and_password()
    {
        var invalid = new AuthRequest { Email = "not-an-email", Password = "" };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(AuthRequest.Email)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(AuthRequest.Password)));
    }

    [Fact]
    public void Persistent_login_requires_exact_origins_and_only_allows_insecure_cookie_in_development()
    {
        var options = new PersistentLoginOptions { AllowedOrigins = ["https://app.example.com"] };
        Assert.True(options.IsValid("Production"));
        Assert.False(new PersistentLoginOptions().IsValid("Development"));
        Assert.False(new PersistentLoginOptions
        {
            AllowedOrigins = ["*"]
        }.IsValid("Production"));
        Assert.False(new PersistentLoginOptions
        {
            AllowedOrigins = ["https://app.example.com/"]
        }.IsValid("Production"));
        Assert.True(new PersistentLoginOptions
        {
            AllowedOrigins = ["http://localhost:8080"],
            AllowInsecureDevelopmentCookie = true
        }.IsValid("Development"));
        Assert.False(new PersistentLoginOptions
        {
            AllowedOrigins = ["http://localhost:8080"],
            AllowInsecureDevelopmentCookie = true
        }.IsValid("Production"));
    }
}
