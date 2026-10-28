using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Incoming email quotation drafts assigned to the authenticated coordinator.</summary>
[ApiController]
[Authorize(Roles = "coordinator")]
[Route("api/v1/quotation-drafts")]
public sealed class QuotationDraftsController(IQuotationDraftService quotationDraftService) : ControllerBase
{
    /// <summary>List drafts created from this coordinator's mailbox.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuotationDraftResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotationDraftResponse>>> List(
        CancellationToken cancellationToken)
    {
        if (!TryGetCoordinatorId(out var coordinatorId))
            return Unauthorized();
        return Ok(await quotationDraftService.ListAsync(coordinatorId, cancellationToken));
    }

    /// <summary>Update the parsed client and machine details of one of this coordinator's drafts.</summary>
    [HttpPut("{draftId:guid}")]
    [ProducesResponseType<QuotationDraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDraftResponse>> Update(
        Guid draftId,
        UpdateQuotationDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCoordinatorId(out var coordinatorId))
            return Unauthorized();

        var draft = await quotationDraftService.UpdateAsync(
            coordinatorId,
            draftId,
            request,
            cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    private bool TryGetCoordinatorId(out Guid coordinatorId) =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out coordinatorId);
}
