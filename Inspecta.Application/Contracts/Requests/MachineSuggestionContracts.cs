namespace Inspecta.Application.Contracts.Requests;

/// <summary>A machine of the request's client that the email seems to ask about.</summary>
/// <param name="EquipmentId">The machine record of the client: use it as the item's <c>equipmentId</c>.</param>
/// <param name="Name">The machine's name.</param>
/// <param name="Quantity">How many the email asks for (1 when it does not say).</param>
/// <param name="Confidence">0 to 1: how sure the match is.</param>
/// <param name="Level"><c>high</c> (put on the request by itself when it has no items), <c>medium</c> (only a suggestion) or <c>ambiguous</c> (several similar machines of the client fit, choose one).</param>
/// <param name="Reason"><c>serial</c> (its serial number or asset tag is in the email), <c>name</c> or <c>model</c>.</param>
/// <param name="Evidence">The line of the email the machine was found in.</param>
public sealed record MachineSuggestionResponse(
    Guid EquipmentId,
    string Name,
    int Quantity,
    double Confidence,
    string Level,
    string Reason,
    string Evidence);
