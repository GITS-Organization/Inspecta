using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Testing helpers for simulating incoming client emails.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/emails")]
public sealed class EmailsController(IInspectionRequestService requestService) : ControllerBase
{
    /// <summary>Generate a realistic client email and save its simulated webhook as an inspection request.</summary>
    [HttpPost("send-test")]
    [ProducesResponseType<TestIncomingEmailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestIncomingEmailResponse>> SendTest(
        TestIncomingEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var coordinatorId))
            return Unauthorized();

        var emailBody = FormatEmail(request);
        var createRequest = new CreateInspectionRequest
        {
            ClientName = request.ClientName,
            Location = request.Location,
            ManagerId = User.IsInRole("admin") ? request.ManagerId : User.GetTenantId(),
            Subject = $"Inspection request for {request.MachineModel}",
            Items =
            [
                new CreateRequestItem
                {
                    Name = request.MachineModel,
                    Type = request.MachineModel,
                    Quantity = 1,
                    SerialNumber = request.SerialNumber,
                    IssueDescription = request.IssueDescription
                }
            ]
        };

        var inspectionRequest = await requestService.CreateFromEmailAsync(
            createRequest,
            request.ContactEmail,
            emailBody,
            coordinatorId,
            createRequest.ManagerId,
            cancellationToken);

        return Created(
            $"/api/v1/requests/{inspectionRequest.Id}",
            new TestIncomingEmailResponse(emailBody, inspectionRequest));
    }

    private static string FormatEmail(TestIncomingEmailRequest request) =>
        $"""
        Hello Inspecta team,

        I'm {request.ClientName}, and we're based in {request.Location}. Could you please arrange an inspection for our {request.MachineModel}{FormatSerialNumber(request.SerialNumber)}?

        The issue we've noticed is: {request.IssueDescription}

        Please let me know when someone is available to visit and if you need any more details.

        Best regards,
        {request.ClientName}
        {request.ContactEmail}
        """;

    private static string FormatSerialNumber(string? serialNumber) =>
        string.IsNullOrWhiteSpace(serialNumber) ? string.Empty : $" (serial number {serialNumber.Trim()})";
}
