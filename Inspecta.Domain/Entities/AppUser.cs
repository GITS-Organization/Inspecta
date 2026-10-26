using Inspecta.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Inspecta.Domain.Entities;

public class AppUser
{
    private static readonly JsonSerializerOptions QualificationJsonOptions = new(JsonSerializerDefaults.Web);

    private AppUser() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? CompanyId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Name => FullName;
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.Inspector;
    public bool IsActive { get; private set; } = true;
    public string QualificationsJson { get; private set; } = "[]";
    public string? SignatureImageId { get; private set; }
    public bool IsOnLeave { get; private set; }
    public DateTimeOffset? LeaveUntil { get; private set; }
    public long HourlyRatePiastres { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; private set; }
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
        long hourlyRatePiastres = 0,
        string? signatureImageId = null,
        string? passwordHash = null,
        bool isActive = true,
        DateTimeOffset? createdAt = null,
        Guid? companyId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (hourlyRatePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company id cannot be empty.", nameof(companyId));

        var user = new AppUser
        {
            CompanyId = companyId,
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            Role = role,
            Phone = Normalize(phone),
            SignatureImageId = Normalize(signatureImageId),
            HourlyRatePiastres = hourlyRatePiastres,
            IsActive = isActive,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
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
        long hourlyRatePiastres,
        string? signatureImageId = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (hourlyRatePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));
        if (isOnLeave && leaveUntil is not null && leaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(leaveUntil));

        var values = qualifications?.ToArray()
            ?? throw new ArgumentNullException(nameof(qualifications));
        if (values.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new ArgumentException("Qualification names are required.", nameof(qualifications));

        FullName = fullName.Trim();
        Phone = Normalize(phone);
        Role = role;
        HourlyRatePiastres = hourlyRatePiastres;
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

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        Role = role;
    }

    public void AssignToCompany(Guid? companyId) => CompanyId = companyId;

    private sealed record QualificationData(string Name, DateOnly? ValidUntil);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
