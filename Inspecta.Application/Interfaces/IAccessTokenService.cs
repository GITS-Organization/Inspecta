using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IAccessTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(AppUser user);
}