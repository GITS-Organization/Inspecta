using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IPasswordService
{
    string Hash(AppUser user, string password);
    bool Verify(AppUser user, string password);
}