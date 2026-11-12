using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public class AuthService : IAuthService
{
    private const int StatusCodes429 = 429;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ILoginAttemptTracker _attempts;
    private readonly ICompanyRepository? _companies;

    // A stand-in account for an email nobody has: its password is checked all the same, so an unknown email takes as long to refuse as a known one.
    private readonly Lazy<(AppUser User, string Hash)> _standIn;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IAccessTokenService accessTokenService,
        ILoginAttemptTracker attempts,
        ICompanyRepository? companies = null)
    {
        _companies = companies;
        _userRepository = userRepository;
        _passwordService = passwordService;
        _accessTokenService = accessTokenService;
        _attempts = attempts;
        _standIn = new Lazy<(AppUser, string)>(() =>
        {
            var user = AppUser.Create("stand-in@invalid.local", "Stand In", UserRole.Inspector);
            var hash = passwordService.Hash(user, Guid.NewGuid().ToString("N"));
            user.SetPasswordHash(hash);
            return (user, hash);
        });
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        // Guessing is slowed down per email, known or not, so the lock tells an attacker nothing about who has an account.
        if (_attempts.LockedFor(email) is { } wait)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
            throw new AuthException(
                "too_many_attempts", $"Too many failed sign-in attempts. Try again in {minutes} minute(s).", StatusCodes429);
        }

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            var (standIn, _) = _standIn.Value;
            _passwordService.Verify(standIn, request.Password);
        }

        if (user is null || !_passwordService.Verify(user, request.Password))
        {
            _attempts.RecordFailure(email);
            throw new AuthException("invalid_credentials", "Email or password is incorrect.");
        }

        _attempts.RecordSuccess(email);

        if (!user.IsActive)
        {
            throw new AuthException("account_disabled", "This account is disabled.", 403);
        }
        await EnsureManagerSubscriptionActiveAsync(user, cancellationToken);

        var (accessToken, accessTokenExpiresAt) = _accessTokenService.Create(user);
        var rawRefreshToken = CreateRefreshToken();
        var refreshToken = RefreshToken.Create(
            user.Id,
            HashToken(rawRefreshToken),
            DateTimeOffset.UtcNow.AddDays(14));

        await _userRepository.SaveRefreshTokenAsync(refreshToken, cancellationToken);
        user.RecordLogin();
        await _userRepository.SaveAsync(user, cancellationToken);

        return new AuthResult(accessToken, rawRefreshToken, accessTokenExpiresAt, await ResponseAsync(user, cancellationToken));
    }

    public async Task<AuthResult> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var existingToken = await _userRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);

        if (existingToken is null)
        {
            throw new AuthException("unauthorized", "Refresh token is invalid or expired.");
        }

        if (existingToken.RevokedAt is not null)
        {
            await _userRepository.RevokeRefreshTokenFamilyAsync(existingToken.FamilyId, cancellationToken);
            throw new AuthException("refresh_token_reused", "Refresh token reuse detected. Sign in again.");
        }

        if (!existingToken.IsActive)
        {
            throw new AuthException("token_expired", "Refresh token is invalid or expired.");
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthException("account_disabled", "This account is disabled.", 403);
        }
        await EnsureManagerSubscriptionActiveAsync(user, cancellationToken);

        var rawRefreshToken = CreateRefreshToken();

        var newRefreshToken = RefreshToken.Create(
            user.Id,
            HashToken(rawRefreshToken),
            DateTimeOffset.UtcNow.AddDays(14),
            existingToken.FamilyId);

        if (!await _userRepository.RotateRefreshTokenAsync(existingToken, newRefreshToken, cancellationToken))
        {
            await _userRepository.RevokeRefreshTokenFamilyAsync(existingToken.FamilyId, cancellationToken);
            throw new AuthException("refresh_token_reused", "Refresh token reuse detected. Sign in again.");
        }

        var (accessToken, accessTokenExpiresAt) = _accessTokenService.Create(user);
        return new AuthResult(accessToken, rawRefreshToken, accessTokenExpiresAt, await ResponseAsync(user, cancellationToken));
    }

    public async Task<AuthUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : await ResponseAsync(user, cancellationToken);
    }

    public async Task ChangeMyPasswordAsync(Guid userId, ChangeMyPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("unauthorized", "The account no longer exists.");

        // Guessing the current password from a stolen session is slowed down exactly like guessing at sign-in.
        if (_attempts.LockedFor(user.Email) is { } wait)
            throw new AuthException(
                "too_many_attempts", $"Too many failed attempts. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} minute(s).", StatusCodes429);
        // 400, not 401: a 401 would tell the app its session has ended.
        if (!_passwordService.Verify(user, request.CurrentPassword))
        {
            _attempts.RecordFailure(user.Email);
            throw new AuthException("current_password_incorrect", "The current password is incorrect.", 400);
        }

        UserManagementService.ValidatePassword(request.NewPassword);
        if (request.NewPassword == request.CurrentPassword)
            throw new AuthException("password_unchanged", "The new password must be different from the current one.", 400);

        user.SetPasswordHash(_passwordService.Hash(user, request.NewPassword));
        await _userRepository.SaveAsync(user, cancellationToken);
        _attempts.RecordSuccess(user.Email);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            await _userRepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        else
            await _userRepository.RevokeOtherRefreshTokensAsync(user.Id, HashToken(request.RefreshToken), cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await _userRepository.RevokeAllRefreshTokensAsync(userId, cancellationToken);
            return;
        }

        var token = await _userRepository.GetRefreshTokenByHashAsync(HashToken(rawRefreshToken), cancellationToken);
        if (token is not null && token.UserId == userId && token.RevokedAt is null)
        {
            await _userRepository.RevokeRefreshTokenAsync(token, cancellationToken);
        }
    }

    private async Task<AuthUserResponse> ResponseAsync(AppUser user, CancellationToken cancellationToken) =>
        ToResponse(user, _companies is not null && user.CompanyId is { } id ? await _companies.GetByIdAsync(id, tracking: false, cancellationToken) : null);

    private static AuthUserResponse ToResponse(AppUser user, Company? company) =>
        new(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.IsActive,
            user.Qualifications.Select(qualification => qualification.Name).ToArray(),
            user.IsOnLeave,
            user.LeaveUntil,
            user.ManagerId,
            user.CreatedById,
            company?.GetEffectiveSubscriptionStatus(),
            company?.SubscriptionEndDate);

    // Everyone but the platform owner works inside a company, and the company must be on with a valid subscription.
    private async Task EnsureManagerSubscriptionActiveAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.Admin || _companies is null)
            return;

        var company = user.CompanyId is Guid companyId
            ? await _companies.GetByIdAsync(companyId, tracking: false, cancellationToken)
            : null;
        if (company is null || !company.IsUsable())
        {
            throw new AuthException(
                "subscription_inactive",
                "The company subscription is suspended or expired.",
                403);
        }
    }

    private static string CreateRefreshToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
