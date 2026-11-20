using Inspecta.Application.Contracts.Auth;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<AuthUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default);
    Task ChangeMyPasswordAsync(Guid userId, ChangeMyPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets the job title and phone the user prints in the signature of their quotations. Empty clears one; too long is <see cref="ArgumentException"/>.</summary>
    Task<AuthUserResponse> UpdateMySignatureAsync(Guid userId, UpdateMySignatureRequest request, CancellationToken cancellationToken = default);
}
