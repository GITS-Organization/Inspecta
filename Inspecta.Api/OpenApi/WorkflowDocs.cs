using System.Globalization;
using Inspecta.Api.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inspecta.Api.OpenApi;

/// <summary>
/// One Swagger section for the whole request and quotation workflow: its endpoints are listed in the order a coordinator
/// uses them, each titled with its step and a short how-to.
/// </summary>
public static class WorkflowDocs
{
    public const string Tag = "Requests & Quotations";
    internal const string OrderExtension = "x-workflow-order";

    private sealed record Step(double Order, string Title, string Guide);

    private static readonly Dictionary<string, Step> Steps = new()
    {
        [Key<RequestsController>(nameof(RequestsController.List))] = new(1, "Step 1 · List requests",
            """
            **When:** the coordinator opens the app. Emails that look like work arrive as requests, the others as "maybe".
            **How:** `GET /requests` shows real requests. Add `triage=maybe` for the maybe tab, `status=quote_draft` (or any status) to filter, `q=` to search, `page`/`pageSize` to page.
            Requests whose quotation is already open leave this list and continue in step 5 (ask for them with an explicit `status` if you need them).
            **Next:** open one (step 3), or confirm a maybe (step 2).
            """),
        [Key<RequestsController>(nameof(RequestsController.Create))] = new(1.1, "Step 1b · Create a request by hand",
            """
            **When:** a request did not come by email. It is then visible to the whole team.
            **How:** send the client, location and equipment items. Email requests are created automatically; you do not call this for them.
            """),
        [Key<RequestsController>(nameof(RequestsController.Summary))] = new(1.2, "Step 1c · Tab counts",
            "**How:** `GET /requests/summary` returns the number of requests, maybe emails and requests per status, for the tab badges."),
        [Key<RequestsController>(nameof(RequestsController.Confirm))] = new(2, "Step 2 · Confirm a maybe email",
            """
            **When:** an email in the maybe tab is really a request.
            **How:** no body. The email becomes a normal request. A maybe nobody confirms is deleted after a week; delete one yourself with `DELETE /requests/{requestId}`.
            """),
        [Key<RequestsController>(nameof(RequestsController.GetById))] = new(3, "Step 3 · Open a request",
            """
            **How:** `GET /requests/{requestId}`. Keep its `updatedAt`: send it back as `expectedUpdatedAt` when saving (step 4) to avoid overwriting newer changes.
            The original mail is under `GET /requests/{requestId}/email` (listed under the extras).
            """),
        [Key<RequestsController>(nameof(RequestsController.GetEmail))] = new(23.5, "Extra · Read the original email",
            "**How:** `GET /requests/{requestId}/email` returns the email the request was created from. `htmlBody` is untrusted sender content: render `textBody` or sanitize it."),
        [Key<RequestsController>(nameof(RequestsController.UpdateDetails))] = new(4, "Step 4 · Fill the request and open the quotation",
            """
            **When:** the coordinator completes the data of the request: client, location and every machine.
            **How:** send `clientName` (or `clientId`), `location`, optional `preferredDate`/`accessNotes` and the whole `items` list. Items with an `id` are updated, items without one are added, items left out are removed. New clients and machines are registered automatically.
            No flag is needed: as soon as the request is complete this same save opens the quotation (`"prepareQuotation": false` only saves, `true` refuses an incomplete request with `409`). The request then leaves the request list and the response has a `quotation` with its `id`, the machines with empty prices and `missingPrices`. Pressing it again refreshes the draft and keeps the prices already typed.
            **Incomplete data:** a save that is not complete yet is just saved, and no quotation is opened. With `prepareQuotation: true` it is refused with `409 request_incomplete` naming the missing fields. A request whose quotation was sent can no longer be edited.
            **Next:** price the machines (step 7).
            """),
        [Key<QuotationsController>(nameof(QuotationsController.List))] = new(5, "Step 5 · List my quotations",
            """
            **When:** the coordinator wants to pick a quotation to work on.
            **How:** `GET /quotations?stage=draft` for the ones still being prepared. Other stages: `sent` (waiting for the client), `accepted`, `rejected`. Use `q=` to search and `page`/`pageSize` to page. Only the newest revision of each request is shown unless `includeSuperseded=true`.
            Pick a row with `canEdit: true`; its `requestId` and `id` are used by the next steps. After acceptance the same list shows who got the job (`inspectorName`, `scheduledAt`) and can be filtered with `assigned` and `inspectorId` (see step 14).
            """),
        [Key<QuotesController>(nameof(QuotesController.Get))] = new(6, "Step 6 · Open a quotation",
            "**How:** `GET /requests/{requestId}/quotes/{quoteId}` returns the machines with their prices, `missingPrices`, notes, scope, the emails sent for it and `canEdit`."),
        [Key<QuotesController>(nameof(QuotesController.Update))] = new(7, "Step 7 · Price the machines and save the draft",
            """
            **When:** the coordinator enters the price of every machine. Save as often as needed while `canEdit` is true.
            **How:** send `items` (every machine of the request exactly once, `unitPricePiastres` may stay empty while drafting; 1 EGP = 100 piastres), plus `validForDays`, `scopeOfWork`, `notes`, `message`, `proposalNumber` and `contactName`.
            **Result:** the total is calculated by the server; `missingPrices` lists the machines still without a price.
            """),
        [Key<QuotesController>(nameof(QuotesController.Pdf))] = new(8, "Step 8 · Preview the PDF",
            "**How:** `GET /requests/{requestId}/quotes/{quoteId}/pdf` returns the PDF the client would receive. Works on drafts too; nothing is sent."),
        [Key<QuotesController>(nameof(QuotesController.Send))] = new(9, "Step 9 · Send the offer",
            """
            **When:** every machine is priced and the preview looks right. Use it for the first offer and for a counter offer.
            **How:** no body. The PDF is emailed as a reply to the sender of the original email. The quotation becomes sent and the request `quote_sent` only after the mail server accepted the message; on `502` nothing changed and you can retry.
            **Then the client answers:** accept (step 10), ask for different prices (step 11) or refuse (step 12).
            """),
        [Key<QuotesController>(nameof(QuotesController.Accept))] = new(10, "Step 10 · Accept",
            """
            **When:** the offer is agreed. Works on a draft (it is sent at the same time, so every machine needs a price) and on a sent quotation.
            **How:** no body. The quotation is closed as `accepted`, the request becomes `quote_accepted` and the PDF is emailed to the client.
            **Next:** assign an inspector (`POST /requests/{requestId}/assignment`).
            """),
        [Key<QuotesController>(nameof(QuotesController.Create))] = new(11, "Step 11 · Counter offer",
            """
            **When:** the client answered a sent offer with other prices.
            **How:** `POST /requests/{requestId}/quotes` (optional `validForDays`). It creates the next revision with the previous prices and replaces the old one in the lists. Change the prices with step 7, preview with step 8 and send it with step 9.
            Also used to open a quotation by hand, but step 4 with `prepareQuotation` already does that.
            """),
        [Key<RequestsController>(nameof(RequestsController.Reject))] = new(12, "Step 12 · Reject",
            """
            **When:** the request is turned down, at any stage before acceptance.
            **How:** body `{ "reason": "..." }` (required). The open quotation is closed as `rejected` and the request becomes `quote_rejected`. **No email is sent.**
            """),

        [Key<QuotesController>(nameof(QuotesController.List))] = new(20, "Extra · All revisions of one request",
            "**How:** `GET /requests/{requestId}/quotes` returns every revision of the request, oldest first. For the day-to-day list use step 5."),
        [Key<QuotesController>(nameof(QuotesController.Decision))] = new(21, "Extra · Record the client's answer without an email",
            "**When:** the client answered by phone. Body `{ \"decision\": \"accepted\" | \"rejected\", \"note\": \"...\" }`. Nothing is emailed; prefer steps 10 and 12."),
        [Key<RequestsController>(nameof(RequestsController.DeleteMaybe))] = new(22, "Extra · Delete a maybe email",
            "**How:** `DELETE /requests/{requestId}`. Only maybe emails can be deleted."),
        [Key<RequestsController>(nameof(RequestsController.AddItem))] = new(23, "Extra · Add one machine",
            "Step 4 saves the whole list at once; use this only to add a single machine."),
        [Key<RequestsController>(nameof(RequestsController.UpdateItem))] = new(24, "Extra · Replace one machine",
            "Step 4 saves the whole list at once; use this only to change a single machine."),
        [Key<RequestsController>(nameof(RequestsController.RemoveItem))] = new(25, "Extra · Remove one machine",
            "Step 4 saves the whole list at once; use this only to remove a single machine."),
        [Key<InspectorsController>(nameof(InspectorsController.List))] = new(13, "Step 13 · Pick an inspector",
            """
            **When:** a quotation was accepted and the job must be given to someone.
            **How:** `GET /inspectors` returns the active inspectors of your team with `isOnLeave` and `activeJobs` (unfinished jobs they already have), so you can pick the least busy one that is available. Admin may add `managerId`.
            **Next:** assign the job (step 14).
            """),
        [Key<RequestsController>(nameof(RequestsController.Assign))] = new(14, "Step 14 · Assign the inspector",
            """
            **When:** the quotation is `accepted` (the request is `quote_accepted`). One inspector gets the whole job.
            **How:** `POST /requests/{requestId}/assignment` with `inspectorId` (from step 13), `scheduledAt` and an optional `note`. The request becomes `assigned`. The owning coordinator, a manager or Admin may do it; an inspector from another team or one on leave at that time is refused.
            **Find the work:** `GET /quotations?stage=accepted&assigned=false` lists accepted quotations still waiting for an inspector, and `GET /quotations?inspectorId=...` lists what one inspector was given. Rows show `inspectorName` and `scheduledAt`.
            **The inspector's side:** the inspector signs in and calls `GET /quotations` to get their own accepted quotations, **without prices** (`amountPiastres` is empty), then `GET /requests/{requestId}` for the machines and the client data.
            """),
        [Key<RequestsController>(nameof(RequestsController.Transition))] = new(31, "After acceptance · Move the job forward",
            "Moves the job through inspector work and certificates. Quotation steps and the assignment are refused with `409`: use the steps above."),
    };

    /// <summary>The setup section: clients with their contacts and the items registered for each client.</summary>
    public const string ConfigTag = "Configurations";

    private static readonly Dictionary<string, Step> ConfigSteps = new()
    {
        [Key<ClientsController>(nameof(ClientsController.Create))] = new(1, "Setup 1 · Create a client",
            """
            **When:** a new customer is set up, before registering its items.
            **How:** send `name`, `isCompany`, `hasContract`, `location` (with optional `latitude`/`longitude`), `certificatesEmail` (where certificates are sent), `taxNumber`, `notes` and the `contacts` (name, email, phone, one `isPrimary`). A coordinator creates the client for their own team; Admin sends `managerId` to choose the team.
            **Next:** register the client's items (setup 5). Clients are also created automatically when a request is saved with a new client name.
            """),
        [Key<ClientsController>(nameof(ClientsController.List))] = new(2, "Setup 2 · List clients",
            "**How:** `GET /clients` returns the clients of your team with their contacts, by name. `includeArchived=true` also returns archived ones."),
        [Key<ClientsController>(nameof(ClientsController.GetById))] = new(3, "Setup 3 · Open a client",
            "**How:** `GET /clients/{clientId}` returns the profile and the contacts. The inspection history is under the extras."),
        [Key<ClientsController>(nameof(ClientsController.Update))] = new(4, "Setup 4 · Update a client and its contacts",
            """
            **How:** `PUT /clients/{clientId}` with the profile fields. Add `contacts` to change them: a contact with an `id` of this client is kept and edited, one without an `id` is added, and one left out is removed. Leave `contacts` out to keep them as they are.
            """),
        [Key<EquipmentController>(nameof(EquipmentController.Create))] = new(5, "Setup 5 · Create an item for a client",
            """
            **When:** a client has equipment to be inspected. Every item belongs to one client, and the client must exist and not be archived.
            **How:** send `clientId`, `name` and, optionally, `itemType`, `category`, `manufacturer`, `model`, `serialNumber`, `capacity`, `locationInsideClientLocation`, `notes`, `labels`, `dates`, `checklist`, `attachments` and **`inspectionStandard`**, the standard the item is inspected against (for example `ASME B30.5`).
            **Note:** items are also registered automatically from the machines of a saved request. Select one in a request with its `equipmentId`.
            """),
        [Key<EquipmentController>(nameof(EquipmentController.List))] = new(6, "Setup 6 · List items",
            "**How:** `GET /equipment?clientId=...` returns the items of one client (or of all your clients without `clientId`). `includeArchived=true` also returns archived ones."),
        [Key<EquipmentController>(nameof(EquipmentController.GetById))] = new(7, "Setup 7 · Open an item",
            "**How:** `GET /equipment/{equipmentId}` returns the item with its `inspectionStandard`, dates and checklist."),
        [Key<EquipmentController>(nameof(EquipmentController.Update))] = new(8, "Setup 8 · Update an item",
            """
            **How:** `PUT /equipment/{equipmentId}` with the same body as the creation. `clientId` must stay the same: an item cannot move to another client. `inspectionStandard` is kept when left out, changed when sent, and cleared when sent as an empty text.
            """),
        [Key<ClientsController>(nameof(ClientsController.GetHistory))] = new(20, "Extra · Client history",
            "**How:** `GET /clients/{clientId}/history` returns the client's previous requests with their certificates."),
        [Key<ClientsController>(nameof(ClientsController.Archive))] = new(21, "Extra · Archive a client",
            "**How:** `DELETE /clients/{clientId}` archives the client; it disappears from the default list and can no longer be edited."),
        [Key<EquipmentController>(nameof(EquipmentController.RecordInspection))] = new(22, "Extra · Record an inspection",
            "**How:** `POST /equipment/{equipmentId}/inspections` with `inspectedAt` and `nextDueAt`; sets the last inspection and the next due date."),
        [Key<EquipmentController>(nameof(EquipmentController.Archive))] = new(23, "Extra · Archive an item",
            "**How:** `DELETE /equipment/{equipmentId}` archives the item; it can no longer be edited."),
    };

    internal static string ConfigDescription =>
        """
        One-time setup of the data the workflow uses, in the order to do it.

        1. **Create a client** with its contacts and emails.
        2. **Create the client's items** (equipment). Each item belongs to one client and carries its **standard of inspection**.
        3. List, open and update clients and items as they change. Contacts are edited together with the client.

        Clients and items are also registered automatically when a request is saved with a new client name or new machines, so this section is for entering them up front or correcting them later.
        """;

    /// <summary>Sections that are not part of the main workflow; they are listed last and say so.</summary>
    internal static readonly IReadOnlyDictionary<string, string> LegacyTags = new Dictionary<string, string>
    {
        ["QuotationDrafts"] = "**Legacy.** The older quotation-draft flow. It is not part of the main workflow: use **Requests & Quotations**."
    };

    internal static string Description =>
        """
        The whole life of a request, in the order a coordinator uses it. Every endpoint below is titled with its step.

        1. **List requests** (`GET /requests`): emails become requests or "maybe".
        2. **Confirm** a maybe email.
        3. **Open** the request.
        4. **Fill the data** (client, location, machines) and open the quotation: `PUT /requests/{id}` with `prepareQuotation: true`.
        5. **List my quotations** (`GET /quotations?stage=draft`) and pick one with `canEdit: true`.
        6. **Open** the quotation, then 7. **price** every machine and save.
        8. **Preview the PDF.**
        9. **Send the offer**; the client then answers:
           - 10. **Accept**: the PDF is emailed and the request is accepted.
           - 11. **Counter offer**: new revision with the old prices, edit (7) and send (9) again.
           - 12. **Reject**: closes the request; no email.
        13. **Pick an inspector** (`GET /inspectors`).
        14. **Assign** the accepted job to that inspector (`POST /requests/{id}/assignment`). Find the work with `GET /quotations?stage=accepted&assigned=false` (waiting) or `?inspectorId=...` (what one inspector has). The inspector sees only their own accepted quotations, without prices.

        Amounts are in piastres (1 EGP = 100 piastres). Statuses and enums are lowercase snake_case in JSON.
        Email requests can only be changed by the coordinator whose mailbox received them; managers and admins can read them.
        """;

    private static string Key<TController>(string action) => $"{typeof(TController).Name}.{action}";

    internal static bool TryGetStep(OperationFilterContext context, out double order, out string title, out string guide, out string tag)
    {
        var type = context.MethodInfo.DeclaringType?.Name;
        var key = $"{type}.{context.MethodInfo.Name}";
        if (type is not null && (Steps.TryGetValue(key, out var step) || ConfigSteps.TryGetValue(key, out step)))
        {
            (order, title, guide) = (step.Order, step.Title, step.Guide);
            tag = Steps.ContainsKey(key) ? Tag : ConfigTag;
            return true;
        }

        (order, title, guide, tag) = (0, string.Empty, string.Empty, string.Empty);
        return false;
    }

    internal static double? OrderOf(OpenApiOperation operation) =>
        operation.Extensions.TryGetValue(OrderExtension, out var value) && value is OpenApiDouble number ? number.Value : null;

    internal static void SetOrder(OpenApiOperation operation, double order) =>
        operation.Extensions[OrderExtension] = new OpenApiDouble(order);

    internal static string Format(double order) => order.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Moves the workflow endpoints into one tag and titles each with its step and how-to.</summary>
public sealed class WorkflowOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!WorkflowDocs.TryGetStep(context, out var order, out var title, out var guide, out var tag)) return;

        var original = string.Join("\n\n", new[] { operation.Summary, operation.Description }
            .Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text!.Trim()));
        operation.Tags = [new OpenApiTag { Name = tag }];
        operation.Summary = title;
        operation.Description = string.IsNullOrEmpty(original)
            ? Dedent(guide)
            : $"{Dedent(guide)}\n\n---\n\n{original}";
        WorkflowDocs.SetOrder(operation, order);
    }

    // Keeps the line breaks of the guide so Swagger renders each "When / How / Next" on its own line.
    private static string Dedent(string guide) =>
        string.Join("\n\n", guide.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Lists the workflow section first and in step order, and describes it.</summary>
public sealed class WorkflowDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // A path keeps all its methods together, so paths follow the first step they serve and methods follow their step.
        var ordered = new OpenApiPaths();
        foreach (var (path, item) in swaggerDoc.Paths
                     .OrderBy(entry => entry.Value.Operations.Values.Select(WorkflowDocs.OrderOf).Min() ?? double.MaxValue)
                     .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var operations = item.Operations
                .OrderBy(entry => WorkflowDocs.OrderOf(entry.Value) ?? double.MaxValue)
                .ToList();
            item.Operations.Clear();
            foreach (var (type, operation) in operations)
            {
                item.Operations[type] = operation;
                operation.Extensions.Remove(WorkflowDocs.OrderExtension);
            }

            ordered.Add(path, item);
        }

        swaggerDoc.Paths = ordered;

        var names = swaggerDoc.Paths.Values.SelectMany(item => item.Operations.Values)
            .SelectMany(operation => operation.Tags.Select(tag => tag.Name))
            .Distinct()
            .OrderBy(name => name == WorkflowDocs.Tag ? 0 : name == WorkflowDocs.ConfigTag ? 1 : WorkflowDocs.LegacyTags.ContainsKey(name) ? 3 : 2)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
        var described = (swaggerDoc.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Description))
            .GroupBy(tag => tag.Name)
            .ToDictionary(group => group.Key, group => group.First().Description);
        swaggerDoc.Tags = names
            .Select(name => new OpenApiTag
            {
                Name = name,
                Description = name == WorkflowDocs.ConfigTag
                    ? WorkflowDocs.ConfigDescription
                    : name == WorkflowDocs.Tag
                    ? WorkflowDocs.Description
                    : WorkflowDocs.LegacyTags.GetValueOrDefault(name) ?? described.GetValueOrDefault(name)
            })
            .ToList();
    }
}
