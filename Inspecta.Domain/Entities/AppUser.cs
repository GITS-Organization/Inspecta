using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirebaseUid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Inspector;
    public bool IsActive { get; set; } = true;
    public string QualificationsJson { get; set; } = "[]";
    public bool IsOnLeave { get; set; }
    public DateTimeOffset? LeaveUntil { get; set; }
    public long HourlyRatePiastres { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
