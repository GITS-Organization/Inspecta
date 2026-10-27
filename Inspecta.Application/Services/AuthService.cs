using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPasswordService _passwordService;
    private readonly IAccessTokenService _accessTokenService;

    public AuthService(
        IUserRepository userRepository,
        ICompanyRepository companyRepository,
        IPasswordService passwordService,
        IAccessTokenService accessTokenService)
    {
        _userRepository = userRepository;
        _companyRepository = companyRepository;
        _passwordService = passwordService;
        _accessTokenService = accessTokenService;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordService.Verify(user, request.Password))
        {
            throw new AuthException("invalid_credentials", "Email or password is incorrect.");
        }

        if (!user.IsActive)
        {
            throw new AuthException("account_disabled", "This account is disabled.", 403);
        }
        await EnsureCompanySubscriptionActiveAsync(user, cancellationToken);

        var (accessToken, accessTokenExpiresAt) = _accessTokenService.Create(user);
        var rawRefreshToken = CreateRefreshToken();
        var refreshToken = RefreshToken.Create(
            user.Id,
            HashToken(rawRefreshToken),
            DateTimeOffset.UtcNow.AddDays(14));

        await _userRepository.SaveRefreshTokenAsync(refreshToken, cancellationToken);
        user.RecordLogin();
        await _userRepository.SaveAsync(user, cancellationToken);

        return new AuthResult(accessToken, rawRefreshToken, accessTokenExpiresAt, ToResponse(user));
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
        await EnsureCompanySubscriptionActiveAsync(user, cancellationToken);

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
        return new AuthResult(accessToken, rawRefreshToken, accessTokenExpiresAt, ToResponse(user));
    }

    public async Task<AuthUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
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

    private static AuthUserResponse ToResponse(AppUser user) =>
        new(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.IsActive,
            user.Qualifications.Select(qualification => qualification.Name).ToArray(),
            user.IsOnLeave,
            user.LeaveUntil,
            user.CompanyId,
            user.CreatedById);

    private async Task EnsureCompanySubscriptionActiveAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        if (user.CompanyId is not Guid companyId)
            return;

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company is null || !company.HasActiveSubscription())
            throw new AuthException(
                "subscription_inactive",
                "The company subscription is suspended, expired, or deleted.",
                403);
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
