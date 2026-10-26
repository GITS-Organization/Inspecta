using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/v1/companies")]
public sealed class CompaniesController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await userManagementService.ListCompaniesAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<CompanyWithManagerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyWithManagerResponse>> Create(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userManagementService.CreateCompanyWithManagerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, result);
    }
}
