using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Api.BackgroundServices;

/// <summary>
/// Mailboxes are read side by side. The same email sent to several mailboxes of one company must become one request, and that check
/// (same Message-ID already imported?) followed by the save is not atomic, so it runs one at a time per company.
/// </summary>
public static class GatedRequestImport
{
    private static readonly KeyedGate<Guid> PerCompany = new();

    public static async Task<bool> SaveAsync(
        IEmailSyncRepository repository,
        InspectionRequest request,
        CancellationToken cancellationToken)
    {
        var saved = false;
        await PerCompany.RunAsync(
            request.ManagerId ?? Guid.Empty,
            async () => saved = await repository.SaveRequestIfNewAsync(request, cancellationToken),
            cancellationToken);
        return saved;
    }
}
