using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.Identity;

public sealed class PlatformIdentityDbContext(DbContextOptions<PlatformIdentityDbContext> options)
    : IdentityUserContext<PlatformUser>(options)
{
    public DbSet<VocabularyProgress.VocabularyAttempt> VocabularyAttempts =>
        Set<VocabularyProgress.VocabularyAttempt>();

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

        builder.Entity<VocabularyProgress.VocabularyAttempt>(attempt =>
        {
            attempt.ToTable("VocabularyAttempts");
            attempt.HasKey(item => item.Id);
            attempt.Property(item => item.UserId).IsRequired();
            attempt.Property(item => item.EventId).HasMaxLength(100).IsRequired();
            attempt.Property(item => item.SessionId).HasMaxLength(100).IsRequired();
            attempt.Property(item => item.ContentId).HasMaxLength(300).IsRequired();
            attempt.Property(item => item.ExerciseType).HasMaxLength(32).IsRequired();
            attempt.Property(item => item.CreatedAt).IsRequired();
            attempt.HasOne<PlatformUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            attempt.HasIndex(item => new { item.UserId, item.EventId })
                .IsUnique();
            attempt.HasIndex(item => new { item.UserId, item.CreatedAt, item.Id });
        });
    }
}
