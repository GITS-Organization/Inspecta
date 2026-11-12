using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>What the sign-in link remembers: who started it, the PKCE secret, and when it stops being valid. It travels encrypted.</summary>
public sealed record MicrosoftConnectionState(Guid CoordinatorId, Guid ManagerId, string CodeVerifier, DateTimeOffset ExpiresAt);

public sealed class MicrosoftMailboxConnectionService(
    IUserRepository userRepository,
    ICoordinatorEmailConfigRepository configRepository,
    ISecretProtector secretProtector,
    IMicrosoftOAuthClient microsoft,
    MicrosoftOAuthOptions options,
    TimeProvider? clock = null,
    ICompanyGate? companyGate = null,
    ICompanyRepository? companies = null) : IMicrosoftMailboxConnectionService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public async Task<StartMicrosoftConnectionResponse> StartAsync(Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        if (!microsoft.IsConfigured)
            throw new AuthException("microsoft_not_configured", "Connecting an Outlook mailbox is not set up on this server yet.", 503);

        var coordinator = await ActiveTeamAsync(coordinatorId, cancellationToken)
            ?? throw new AuthException("coordinator_not_found", "An active coordinator of an active team is needed.", 404);

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = secretProtector.Protect(JsonSerializer.Serialize(
            new MicrosoftConnectionState(coordinatorId, coordinator.ManagerId!.Value, verifier, time.GetUtcNow().Add(StateLifetime))));
        return new StartMicrosoftConnectionResponse(microsoft.BuildAuthorizationUrl(state, challenge));
    }

    public async Task<MicrosoftConnectionResult> CompleteAsync(string? state, string? code, string? error, CancellationToken cancellationToken = default)
    {
        if (!microsoft.IsConfigured)
            return Failed("microsoft_not_configured", "Connecting an Outlook mailbox is not set up on this server yet.");

        var remembered = ReadState(state);
        if (remembered is null || remembered.ExpiresAt <= time.GetUtcNow())
            return Failed("invalid_oauth_state", "This sign-in link is not valid any more. Start connecting the mailbox again.");

        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
            return Failed("microsoft_sign_in_failed", "The Microsoft sign-in was cancelled or refused. Nothing was connected.");

        var team = await ActiveTeamAsync(remembered.CoordinatorId, cancellationToken);
        if (team is null || team.ManagerId != remembered.ManagerId)
            return Failed("invalid_oauth_state", "This sign-in link is not valid any more. Start connecting the mailbox again.");

        MicrosoftTokens tokens;
        try
        {
            tokens = await microsoft.ExchangeCodeAsync(code, remembered.CodeVerifier, cancellationToken);
        }
        catch (Exception exception) when (exception is MicrosoftTokenRejectedException or HttpRequestException)
        {
            return Failed("microsoft_sign_in_failed", "Microsoft did not accept the sign-in. Try connecting the mailbox again.");
        }

        if (string.IsNullOrWhiteSpace(tokens.RefreshToken) || string.IsNullOrWhiteSpace(tokens.MailboxAddress))
            return Failed("microsoft_sign_in_failed", "Microsoft did not allow the access the mailbox needs. Try again and accept every permission.");

        var companyDomain = DomainOf(await CompanyMailAddressAsync(team, cancellationToken));
        if (!options.AllowAnyDomain && !string.Equals(DomainOf(tokens.MailboxAddress), companyDomain, StringComparison.OrdinalIgnoreCase))
            return Failed("mailbox_domain_mismatch", $"This mailbox is on {DomainOf(tokens.MailboxAddress)}, but the company mailboxes are on {companyDomain}.");

        var encrypted = secretProtector.Protect(tokens.RefreshToken);
        var microsoftTenant = string.IsNullOrWhiteSpace(tokens.MicrosoftTenantId) ? "unknown" : tokens.MicrosoftTenantId;
        var config = await configRepository.GetByCoordinatorAsync(remembered.CoordinatorId, cancellationToken);
        if (config is null)
            config = CoordinatorEmailConfig.CreateMicrosoft(remembered.CoordinatorId, remembered.ManagerId, tokens.MailboxAddress, encrypted, microsoftTenant);
        else
            config.ConnectMicrosoft(tokens.MailboxAddress, encrypted, microsoftTenant);
        await configRepository.SaveAsync(config, cancellationToken);

        return new MicrosoftConnectionResult(true, $"The mailbox {tokens.MailboxAddress} is connected. You can close this page.", tokens.MailboxAddress);
    }


    // The company's mailboxes are on the domain of its technical manager (the default reviewer); a company without one falls back to the
    // coordinator's own address.
    private async Task<string> CompanyMailAddressAsync(AppUser coordinator, CancellationToken cancellationToken)
    {
        if (companies is not null && coordinator.CompanyId is { } id &&
            await companies.GetByIdAsync(id, tracking: false, cancellationToken) is { DefaultReviewerId: { } reviewerId } &&
            await userRepository.GetByIdAsync(reviewerId, cancellationToken) is { } reviewer)
            return reviewer.Email;
        return coordinator.Email;
    }

    private async Task<AppUser?> ActiveTeamAsync(Guid coordinatorId, CancellationToken cancellationToken)
    {
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken);
        if (coordinator is not { Role: UserRole.Coordinator, IsActive: true, ManagerId: Guid managerId }) return null;
        if (companyGate is not null && !await companyGate.IsUsableAsync(managerId, cancellationToken)) return null;
        return coordinator;
    }

    // A state that cannot be read (changed, cut short, or made by another key) is simply not ours.
    private MicrosoftConnectionState? ReadState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        try
        {
            return JsonSerializer.Deserialize<MicrosoftConnectionState>(secretProtector.Unprotect(state));
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or JsonException or FormatException)
        {
            return null;
        }
    }

    private static MicrosoftConnectionResult Failed(string code, string message) => new(false, message, null, code);

    private static string DomainOf(string address)
    {
        var at = address.LastIndexOf('@');
        return at < 0 ? address.Trim() : address[(at + 1)..].Trim();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
