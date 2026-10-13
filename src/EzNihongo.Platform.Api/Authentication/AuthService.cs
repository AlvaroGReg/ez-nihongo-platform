using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EzNihongo.Platform.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace EzNihongo.Platform.Api.Authentication;

public sealed class AuthService(
    UserManager<PlatformUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthOperationResult> RegisterAsync(AuthRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new PlatformUser { UserName = email, Email = email };
        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return AuthOperationResult.Conflict();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                return AuthOperationResult.Conflict();

            return AuthOperationResult.Invalid(result.Errors
                .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "request")
                .ToDictionary(group => group.Key, group => group.Select(error => SafeIdentityError(error.Code)).Distinct().ToArray()));
        }

        return AuthOperationResult.Success(CreateToken(user));
    }

    public async Task<AuthOperationResult> LoginAsync(AuthRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return AuthOperationResult.Invalid(new Dictionary<string, string[]> { ["credentials"] = ["Invalid email or password."] });

        return AuthOperationResult.Success(CreateToken(user));
    }

    private AuthTokenResponse CreateToken(PlatformUser user)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_jwt.GetSigningKeyBytes()),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);
        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthTokenResponse(
            new AuthUserResponse(user.Id, user.Email!),
            encodedToken,
            "Bearer",
            expires);
    }

    private static string SafeIdentityError(string code) => code switch
    {
        "PasswordTooShort" => "Password does not meet the minimum length requirement.",
        "PasswordRequiresDigit" => "Password must contain a digit.",
        "PasswordRequiresLower" => "Password must contain a lowercase letter.",
        "PasswordRequiresUpper" => "Password must contain an uppercase letter.",
        "PasswordRequiresNonAlphanumeric" => "Password must contain a non-alphanumeric character.",
        "InvalidEmail" => "A valid email address is required.",
        _ => "The request does not meet the account requirements."
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
