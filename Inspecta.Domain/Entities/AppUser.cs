using Inspecta.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SubscriptionState = Inspecta.Domain.Enums.SubscriptionStatus;

namespace Inspecta.Domain.Entities;

public class AppUser
{
    private static readonly JsonSerializerOptions QualificationJsonOptions = new(JsonSerializerDefaults.Web);

    private AppUser() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? ManagerId { get; private set; }
    public Guid? CreatedById { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Name => FullName;
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.Inspector;
    public bool IsActive { get; private set; } = true;
    public string QualificationsJson { get; private set; } = "[]";
    public string? SignatureImageId { get; private set; }

    /// <summary>The hand-drawn signature (normalised strokes) put on the certificates this user approves. Empty until saved.</summary>
    public string? SignatureStrokesJson { get; private set; }

    public void SetSignature(string? strokesJson) =>
        SignatureStrokesJson = string.IsNullOrWhiteSpace(strokesJson) ? null : strokesJson.Trim();
    public bool IsOnLeave { get; private set; }
    public DateTimeOffset? LeaveUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; private set; }
    public SubscriptionState? SubscriptionStatus { get; private set; }
    public DateTimeOffset? SubscriptionEndDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    [NotMapped]
    public IReadOnlyList<UserQualification> Qualifications => UserQualification.ParseJson(QualificationsJson);

    public void SetQualifications(IEnumerable<UserQualification> qualifications)
    {
        ArgumentNullException.ThrowIfNull(qualifications);
        var values = qualifications.ToArray();
        if (values.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new ArgumentException("Qualification names are required.", nameof(qualifications));
        QualificationsJson = JsonSerializer.Serialize(
            values.Select(item => new QualificationData(item.Name.Trim(), item.ValidUntil)),
            QualificationJsonOptions);
    }

    public static AppUser Create(
        string email,
        string fullName,
        UserRole role,
        string? phone = null,
        IEnumerable<UserQualification>? qualifications = null,
        string? signatureImageId = null,
        string? passwordHash = null,
        bool isActive = true,
        DateTimeOffset? createdAt = null,
        Guid? managerId = null,
        DateTimeOffset? subscriptionEndDate = null,
        Guid? createdById = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (managerId == Guid.Empty)
            throw new ArgumentException("Manager id cannot be empty.", nameof(managerId));
        if (managerId is not null && role is (UserRole.Admin or UserRole.TechnicalManager))
            throw new ArgumentException("Only a team member can be assigned to a technical manager.", nameof(managerId));
        if (subscriptionEndDate is not null && role != UserRole.TechnicalManager)
            throw new ArgumentException("Only a technical manager can have a subscription.", nameof(subscriptionEndDate));

        var user = new AppUser
        {
            ManagerId = managerId,
            CreatedById = createdById,
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            Role = role,
            Phone = Normalize(phone),
            SignatureImageId = Normalize(signatureImageId),
            IsActive = isActive,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            SubscriptionStatus = role == UserRole.TechnicalManager ? SubscriptionState.Active : null,
            SubscriptionEndDate = subscriptionEndDate
        };
        if (qualifications is not null)
            user.SetQualifications(qualifications);
        if (passwordHash is not null)
            user.SetPasswordHash(passwordHash);
        return user;
    }

    public void UpdateProfile(
        string fullName,
        string? phone,
        IEnumerable<UserQualification> qualifications,
        UserRole role,
        bool isOnLeave,
        DateTimeOffset? leaveUntil,
        string? signatureImageId = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (isOnLeave && leaveUntil is not null && leaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(leaveUntil));

        var values = qualifications?.ToArray()
            ?? throw new ArgumentNullException(nameof(qualifications));
        if (values.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new ArgumentException("Qualification names are required.", nameof(qualifications));

        FullName = fullName.Trim();
        Phone = Normalize(phone);
        ChangeRole(role);
        IsOnLeave = isOnLeave;
        LeaveUntil = isOnLeave ? leaveUntil : null;
        SignatureImageId = Normalize(signatureImageId);
        SetQualifications(values);
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    public void RecordLogin(DateTimeOffset? loggedInAt = null) =>
        LastLoginAt = loggedInAt ?? DateTimeOffset.UtcNow;

    public void SetLeave(DateTimeOffset? until)
    {
        IsOnLeave = until.HasValue && until.Value > DateTimeOffset.UtcNow;
        LeaveUntil = IsOnLeave ? until : null;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SoftDelete(DateTimeOffset? deletedAt = null)
    {
        if (IsDeleted)
            return;

        IsActive = false;
        IsDeleted = true;
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
    }

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (Role == UserRole.TechnicalManager && role != UserRole.TechnicalManager)
        {
            SubscriptionStatus = null;
            SubscriptionEndDate = null;
        }
        else if (Role != UserRole.TechnicalManager && role == UserRole.TechnicalManager)
        {
            SubscriptionStatus = SubscriptionState.Expired;
            SubscriptionEndDate = null;
        }
        Role = role;
        if (role is UserRole.Admin or UserRole.TechnicalManager)
            ManagerId = null;
    }

    public void AssignToManager(Guid? managerId)
    {
        if (managerId == Guid.Empty)
            throw new ArgumentException("Manager id cannot be empty.", nameof(managerId));
        if (managerId == Id)
            throw new ArgumentException("A user cannot manage themselves.", nameof(managerId));
        if (managerId is not null && Role is (UserRole.Admin or UserRole.TechnicalManager))
            throw new ArgumentException("Admins and technical managers cannot be assigned to another manager.", nameof(managerId));

        ManagerId = managerId;
    }

    public void UpdateSubscription(SubscriptionState status, DateTimeOffset? endDate)
    {
        if (Role != UserRole.TechnicalManager)
            throw new InvalidOperationException("Only a technical manager can have a subscription.");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        SubscriptionStatus = status;
        SubscriptionEndDate = endDate;
    }

    public SubscriptionState? GetEffectiveSubscriptionStatus(DateTimeOffset? now = null)
    {
        if (Role != UserRole.TechnicalManager || SubscriptionStatus is null)
            return null;
        if (SubscriptionStatus != SubscriptionState.Active)
            return SubscriptionStatus;
        return SubscriptionEndDate is { } endDate && endDate <= (now ?? DateTimeOffset.UtcNow)
            ? SubscriptionState.Expired
            : SubscriptionState.Active;
    }

    public bool HasActiveSubscription(DateTimeOffset? now = null) =>
        GetEffectiveSubscriptionStatus(now) == SubscriptionState.Active;

    private sealed record QualificationData(string Name, DateOnly? ValidUntil);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
