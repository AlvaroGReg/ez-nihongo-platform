using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.Identity;

public sealed class PlatformIdentityDbContext(DbContextOptions<PlatformIdentityDbContext> options)
    : IdentityUserContext<PlatformUser>(options)
{
    public DbSet<VocabularyProgress.VocabularyAttempt> VocabularyAttempts =>
        Set<VocabularyProgress.VocabularyAttempt>();

    public DbSet<PersistentLoginSession> PersistentLoginSessions => Set<PersistentLoginSession>();

    public DbSet<KnownContent.UserKnownContent> UserKnownContent => Set<KnownContent.UserKnownContent>();

    public DbSet<DailyRegistrationUsage> DailyRegistrationUsages => Set<DailyRegistrationUsage>();

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

        builder.Entity<DailyRegistrationUsage>(usage =>
        {
            usage.ToTable("DailyRegistrationUsage");
            usage.HasKey(item => item.Date);
            usage.Property(item => item.Date).HasColumnType("date");
            usage.Property(item => item.Count).IsRequired();
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

        builder.Entity<PersistentLoginSession>(session =>
        {
            session.ToTable("PersistentLoginSessions");
            session.HasKey(item => item.Id);
            session.Property(item => item.UserId).IsRequired();
            session.Property(item => item.TokenHash).HasColumnType("bytea").IsRequired();
            session.Property(item => item.CreatedAt).IsRequired();
            session.Property(item => item.ExpiresAt).IsRequired();
            session.HasOne<PlatformUser>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            session.HasOne<PersistentLoginSession>().WithMany().HasForeignKey(item => item.ReplacedById)
                .OnDelete(DeleteBehavior.SetNull);
            session.HasIndex(item => item.TokenHash).IsUnique();
            session.HasIndex(item => item.FamilyId);
            session.HasIndex(item => item.UserId);
        });

        builder.Entity<KnownContent.UserKnownContent>(item =>
        {
            item.ToTable("UserKnownContent", table => table.HasCheckConstraint(
                "CK_UserKnownContent_SourceShape",
                "(\"Source\" = 'catalog' AND \"CatalogContentId\" IS NOT NULL AND \"NormalizedWriting\" IS NULL AND \"NormalizedReading\" IS NULL AND \"Word\" IS NULL AND \"Reading\" IS NULL AND \"Romaji\" IS NULL AND \"Meaning\" IS NULL AND \"Kanji\" IS NULL AND \"Readings\" IS NULL AND \"Meanings\" IS NULL) OR (\"Source\" = 'custom' AND \"CatalogContentId\" IS NULL AND \"NormalizedWriting\" IS NOT NULL AND ((\"ContentType\" = 'vocabulary' AND \"NormalizedReading\" IS NOT NULL AND \"Word\" IS NOT NULL AND \"Reading\" IS NOT NULL AND \"Romaji\" IS NOT NULL AND \"Meaning\" IS NOT NULL AND \"Kanji\" IS NULL AND \"Readings\" IS NULL AND \"Meanings\" IS NULL) OR (\"ContentType\" = 'kanji' AND \"NormalizedReading\" IS NULL AND \"Kanji\" IS NOT NULL AND \"Readings\" IS NOT NULL AND \"Meanings\" IS NOT NULL AND \"Word\" IS NULL AND \"Reading\" IS NULL AND \"Romaji\" IS NULL AND \"Meaning\" IS NULL)))"));
            item.HasKey(value => value.Id);
            item.Property(value => value.UserId).IsRequired();
            item.Property(value => value.ContentType).HasMaxLength(16).IsRequired();
            item.Property(value => value.Source).HasMaxLength(16).IsRequired();
            item.Property(value => value.CatalogContentId).HasMaxLength(300);
            item.Property(value => value.Word).HasMaxLength(100);
            item.Property(value => value.Reading).HasMaxLength(200);
            item.Property(value => value.Romaji).HasMaxLength(200);
            item.Property(value => value.Meaning).HasMaxLength(500);
            item.Property(value => value.Kanji).HasMaxLength(100);
            item.Property(value => value.NormalizedWriting).HasMaxLength(300);
            item.Property(value => value.NormalizedReading).HasMaxLength(600);
            item.Property(value => value.CreatedAt).IsRequired();
            item.HasOne<PlatformUser>().WithMany().HasForeignKey(value => value.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            item.HasIndex(value => new { value.UserId, value.ContentType, value.CatalogContentId })
                .IsUnique().HasFilter("\"Source\" = 'catalog'");
            item.HasIndex(value => new { value.UserId, value.ContentType, value.NormalizedWriting, value.NormalizedReading })
                .IsUnique().HasFilter("\"Source\" = 'custom' AND \"ContentType\" = 'vocabulary'");
            item.HasIndex(value => new { value.UserId, value.ContentType, value.NormalizedWriting })
                .IsUnique().HasFilter("\"Source\" = 'custom' AND \"ContentType\" = 'kanji'");
            item.HasIndex(value => new { value.UserId, value.ContentType, value.CreatedAt, value.Id });
        });
    }
}
