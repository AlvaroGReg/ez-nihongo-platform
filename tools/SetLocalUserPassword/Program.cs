using EzNihongo.Platform.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: SetLocalUserPassword <userName>");
    return 2;
}

var userName = args[0];
var password = Environment.GetEnvironmentVariable("EZNIHONGO_RESET_PASSWORD");
Environment.SetEnvironmentVariable("EZNIHONGO_RESET_PASSWORD", null);
if (string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine("The reset password was not provided by the PowerShell script.");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("Postgres__ConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var postgresPassword = Environment.GetEnvironmentVariable("EZNIHONGO_POSTGRES_PASSWORD");
    if (string.IsNullOrWhiteSpace(postgresPassword))
    {
        Console.Error.WriteLine("Set Postgres__ConnectionString or provide POSTGRES_PASSWORD in the workspace .env file.");
        return 2;
    }

    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = "localhost",
        Port = 5432,
        Database = "eznihongo",
        Username = "eznihongo",
        Password = postgresPassword
    }.ConnectionString;
}

var services = new ServiceCollection();
services.AddLogging();
services.AddDataProtection();
services.AddDbContext<PlatformIdentityDbContext>(options => options.UseNpgsql(connectionString));
services.AddIdentityCore<PlatformUser>()
    .AddEntityFrameworkStores<PlatformIdentityDbContext>()
    .AddDefaultTokenProviders();

using var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var database = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();
await database.Database.MigrateAsync();

var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
var user = await userManager.FindByNameAsync(userName);
if (user is null)
{
    Console.Error.WriteLine($"No user was found with userName '{userName}'.");
    return 1;
}

var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
var result = await userManager.ResetPasswordAsync(user, resetToken, password);
password = null;

if (!result.Succeeded)
{
    Console.Error.WriteLine("The password was not changed:");
    foreach (var error in result.Errors)
        Console.Error.WriteLine($"- {error.Code}: {error.Description}");

    return 1;
}

Console.WriteLine($"Password reset successfully for '{user.UserName}'.");
return 0;
