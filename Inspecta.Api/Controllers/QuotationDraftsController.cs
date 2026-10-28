using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Incoming email quotation drafts scoped to the authenticated user and tenant.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/quotation-drafts")]
public sealed class QuotationDraftsController(IQuotationDraftService quotationDraftService) : ControllerBase
{
    /// <summary>
    /// List quotation drafts: Coordinators see their own, Technical Managers see their team,
    /// and Admins see all tenants or a selected tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuotationDraftResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<QuotationDraftResponse>>> List(
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("admin"))
            return Ok(await quotationDraftService.ListAsync(managerId, null, cancellationToken));

        if (!TryGetUserId(out var userId) ||
            !Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId))
            return Unauthorized();

        if (User.IsInRole("technical_manager"))
        {
            if (managerId is Guid requestedTenantId && requestedTenantId != tenantId)
                throw new AuthException("forbidden", "Technical managers can only view drafts for their own team.", 403);

            return Ok(await quotationDraftService.ListAsync(tenantId, null, cancellationToken));
        }

        if (User.IsInRole("coordinator"))
        {
            if (managerId is not null && managerId != tenantId)
                throw new AuthException("forbidden", "Coordinators can only view drafts for their own team.", 403);

            return Ok(await quotationDraftService.ListAsync(tenantId, userId, cancellationToken));
        }

        throw new AuthException("forbidden", "This role cannot view quotation drafts.", 403);
    }

    /// <summary>Update the parsed client and machine details of one of this coordinator's drafts.</summary>
    [HttpPut("{draftId:guid}")]
    [Authorize(Roles = "coordinator")]
    [ProducesResponseType<QuotationDraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDraftResponse>> Update(
        Guid draftId,
        UpdateQuotationDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var coordinatorId))
            return Unauthorized();

        var draft = await quotationDraftService.UpdateAsync(
            coordinatorId,
            draftId,
            request,
            cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    /// <summary>Set a draft to CounterOffered, Accepted, or Rejected.</summary>
    [HttpPost("{draftId:guid}/decision")]
    [Authorize(Roles = "coordinator")]
    [ProducesResponseType<QuotationDraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDraftResponse>> Decide(
        Guid draftId,
        QuotationDraftDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var coordinatorId))
            return Unauthorized();

        var draft = await quotationDraftService.DecideAsync(
            coordinatorId,
            draftId,
            request,
            cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);
}
