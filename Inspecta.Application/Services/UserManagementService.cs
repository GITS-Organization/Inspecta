using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class UserManagementService(IUserRepository userRepository, IPasswordService passwordService) : IUserManagementService
{
    public async Task<AuthUserResponse> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePassword(request.Password);
        if (!Enum.IsDefined(request.Role)) throw new ArgumentException("User role is invalid.", nameof(request.Role));
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userRepository.GetByEmailAsync(email, cancellationToken) is not null ||
            await userRepository.GetByFirebaseUidAsync(request.FirebaseUid, cancellationToken) is not null)
        {
            throw new AuthException("account_exists", "Email or Firebase UID is already registered.", 409);
        }
        if (request.HourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));

        var qualifications = request.Qualifications
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => UserQualification.Create(value))
            .ToArray();
        var user = AppUser.Create(
            request.FirebaseUid,
            email,
            request.FullName,
            request.Role,
            qualifications: qualifications,
            hourlyRatePiastres: request.HourlyRatePiastres);
        user.SetPasswordHash(passwordService.Hash(user, request.Password));
        await userRepository.AddAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<AuthUserResponse> UpdateProfileAsync(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.HourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));
        if (!Enum.IsDefined(request.Role)) throw new ArgumentException("User role is invalid.", nameof(request.Role));
        if (request.IsOnLeave && request.LeaveUntil is not null && request.LeaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(request.LeaveUntil));
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);
        var qualifications = request.Qualifications
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => UserQualification.Create(value))
            .ToArray();
        user.UpdateProfile(
            request.FullName,
            user.Phone,
            qualifications,
            request.Role,
            request.IsOnLeave,
            request.LeaveUntil,
            request.HourlyRatePiastres,
            user.SignatureImageId);
        await userRepository.SaveAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await userRepository.DeactivateAsync(userId, cancellationToken))
        {
            throw new AuthException("user_not_found", "User was not found or is already inactive.", 404);
        }
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new AuthException("password_policy_failed", "Password must be at least 12 characters and include uppercase, lowercase, numeric, and symbol characters.", 400);
        }
    }

    private static AuthUserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.FirebaseUid,
        user.Email,
        user.FullName,
        user.Role,
        user.IsActive,
        user.Qualifications.Select(qualification => qualification.Name).ToArray(),
        user.IsOnLeave,
        user.LeaveUntil,
        user.HourlyRatePiastres);
}