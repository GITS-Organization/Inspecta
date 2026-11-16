using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Try mailbox settings before the coordinator they are for exists.</summary>
[ApiController]
[Route("api/v1/company/email-config")]
public sealed class CompanyMailboxTestController(ICoordinatorEmailConfigService configService) : ControllerBase
{
    /// <summary>Try the settings typed in while adding a coordinator, without saving anything: can the server be reached, does the sign-in work, can the inbox be read, can mail be sent.</summary>
    /// <remarks>
    /// The same body and the same answer as testing the mailbox of an existing coordinator (<c>POST /company/users/{id}/email-config/test</c>): always <c>200</c> with the four
    /// steps (<c>reach</c>, <c>sign_in</c>, <c>read</c>, <c>send</c>), each <c>ok</c>, <c>failed</c> (with <c>message</c> and <c>hint</c>) or <c>skipped</c>. The password is needed.
    /// Nothing is stored and no email is sent. Trying a mailbox is limited to ten tries a minute for a company, whichever way (<c>429 too_many_tries</c> after that).
    /// The platform owner passes <c>managerId</c>.
    /// </remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPost("test")]
    [ProducesResponseType<MailboxCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MailboxCheckResponse>> Test(
        CoordinatorEmailConfigRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await configService.TestNewAsync(ResolveManagerId(managerId), request, cancellationToken));

    private Guid ResolveManagerId(Guid? requestedManagerId)
    {
        if (User.IsInRole("admin"))
            return requestedManagerId ?? throw new AuthException("manager_required", "Admins must provide a managerId.", 400);

        var companyId = User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
        if (requestedManagerId is Guid managerId && managerId != companyId)
            throw new AuthException("forbidden", "You can only manage your own company.", 403);
        return companyId;
    }
}
