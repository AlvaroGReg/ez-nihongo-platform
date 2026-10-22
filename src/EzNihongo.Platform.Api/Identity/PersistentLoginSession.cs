namespace EzNihongo.Platform.Api.Identity;

public sealed class PersistentLoginSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public Guid FamilyId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RotatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}
