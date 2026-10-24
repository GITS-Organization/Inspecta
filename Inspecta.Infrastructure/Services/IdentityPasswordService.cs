using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Inspecta.Infrastructure.Services;

public sealed class IdentityPasswordService : IPasswordService
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string Hash(AppUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(AppUser user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}