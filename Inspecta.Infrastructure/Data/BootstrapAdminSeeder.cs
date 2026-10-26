using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Inspecta.Infrastructure.Data;

public sealed class BootstrapAdminSeeder(InspectaDbContext dbContext, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        var firebaseUid = configuration["BootstrapAdmin:FirebaseUid"];
        var fullName = configuration["BootstrapAdmin:FullName"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Bootstrap admin requires Email, Password, and FullName settings together.");
        }

        UserManagementService.ValidatePassword(password);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(
                user => user.Email == normalizedEmail ||
                        (firebaseUid != null && user.FirebaseUid == firebaseUid),
                cancellationToken))
        {
            return;
        }

        var user = AppUser.Create(firebaseUid, normalizedEmail, fullName, UserRole.Admin);
        user.SetPasswordHash(new PasswordHasher<AppUser>().HashPassword(user, password));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}