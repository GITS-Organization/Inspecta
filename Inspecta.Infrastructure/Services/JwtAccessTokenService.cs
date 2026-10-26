using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Inspecta.Infrastructure.Services;

public sealed class JwtAccessTokenService(IConfiguration configuration) : IAccessTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) Create(AppUser user)
    {
        var issuer = RequiredSetting("Jwt:Issuer");
        var audience = RequiredSetting("Jwt:Audience");
        var secret = RequiredSetting("Jwt:SigningKey");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 10));
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", RoleName(user.Role)),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt.UtcDateTime, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private string RequiredSetting(string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Required configuration '{key}' is missing.");

    private static string RoleName(Inspecta.Domain.Enums.UserRole role) => role switch
    {
        Inspecta.Domain.Enums.UserRole.Coordinator => "coordinator",
        Inspecta.Domain.Enums.UserRole.Inspector => "inspector",
        Inspecta.Domain.Enums.UserRole.TechnicalManager => "technical_manager",
        Inspecta.Domain.Enums.UserRole.Admin => "admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown user role.")
    };
}