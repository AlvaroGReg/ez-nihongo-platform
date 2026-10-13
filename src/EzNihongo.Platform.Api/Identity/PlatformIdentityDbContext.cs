using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.Identity;

public sealed class PlatformIdentityDbContext(DbContextOptions<PlatformIdentityDbContext> options)
    : IdentityUserContext<PlatformUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlatformUser>(user =>
        {
            user.ToTable("Users");
            user.HasIndex(account => account.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
    }
}
