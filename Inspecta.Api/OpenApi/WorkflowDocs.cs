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
            Pick a row with `canEdit: true`; its `requestId` and `id` are used by the next steps. After acceptance the same list shows who got the job (`inspectorName`, `scheduledAt`) and can be filtered with `assigned` and `inspectorId` (see the Quotation queue section).
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
            **How:** no body. The quotation is closed as `accepted`, the request becomes `unassigned` and the PDF is emailed to the client.
            **Next:** the quotation appears in the **Quotation queue** section, where it is given to an inspector.
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
        [Key<RequestsController>(nameof(RequestsController.Transition))] = new(31, "After acceptance · Move the job forward",
            "Moves a job to `task_accepted` or `in_progress` (the inspector's own jobs). Quotation steps, the assignment and every **certificate** state (`certificate_submitted`, `certificate_returned`, `certificate_approved`, `sent_to_client`) are refused with `409`: those follow from the steps of the **Quotation queue** section (submit, approve-all, return-to-inspector, deliver-to-client), so a job never reaches review without its certificates."),
    };

    /// <summary>What happens to a quotation once the client accepted it: the queues, the inspector and the date.</summary>
    public const string QueueTag = "Quotation queue";

    private static readonly Dictionary<string, Step> QueueSteps = new()
    {
        [Key<QuotationQueueController>(nameof(QuotationQueueController.List))] = new(1, "Queue 1 · List the queue (one endpoint, filtered by status)",
            """
            **When:** a quotation was accepted (workflow step 10), and whenever the app shows the work after acceptance.
            **How:** `GET /quotation-queue?status=...`. `status` picks the queue; leave it out to get all of them, and every row says which one it is in with `queue`:
            - `status=unassigned`: accepted, waiting for an inspector (request status `unassigned`).
            - `status=assigned`: given to an inspector, inspection not finished (request status `assigned`, `task_accepted` or `in_progress`).
            - `status=certify`: inspection done, certificate being prepared, reviewed or sent (request status `certificate_submitted`, `certificate_returned`, `certificate_approved` or `sent_to_client`).
            - one part of `certify`: `status=review` (technical manager), `status=returned` (inspector), `status=approved` (coordinator), `status=delivered`.
            Other filters: `inspectorId`, `q` (client, quotation number, location), `managerId` (Admin), `page`/`pageSize`. Each row has `requestId`, the client, the location, the price, `inspectorName` and `scheduledAt`. Open a row with `GET /requests/{requestId}/quotes/{id}`.
            **The inspector's side:** an inspector gets only their own jobs here, **without prices** (`amountPiastres` is empty), so `unassigned` is always empty for them. Open one with `GET /requests/{requestId}` for the machines and the company data. The coordinator keeps full access to every quotation of their requests.
            **Next:** pick an inspector (queue 2) for an `unassigned` row.
            """),
        [Key<InspectorsController>(nameof(InspectorsController.List))] = new(2, "Queue 2 · Pick an inspector",
            """
            **How:** `GET /inspectors` returns the active inspectors of your team with `isOnLeave` and `activeJobs` (unfinished jobs they already have), so you can pick the least busy one that is available. Admin may add `managerId`.
            """),
        [Key<RequestsController>(nameof(RequestsController.Assign))] = new(3, "Queue 3 · Assign the job to an inspector",
            """
            **When:** the request is `unassigned`. One inspector gets the whole job. It can be called again, while the job has not started (`assigned` or `task_accepted`), to hand it to someone else.
            **How:** `POST /requests/{requestId}/assignment` with `inspectorId` (from queue 2), `scheduledAt` and an optional `note`. The request becomes `assigned` and the quotation moves from `status=unassigned` to `status=assigned`. The owning coordinator, a manager or Admin may do it; an inspector from another team or one on leave at that time is refused.
            """),
        [Key<QuotationQueueController>(nameof(QuotationQueueController.ChangeInspectionDate))] = new(4, "Queue 4 · Change the inspection date",
            """
            **When:** the inspection has to move. This is the only change an inspector can make to a job; the coordinator, a manager or Admin can do it too.
            **How:** `PUT /quotation-queue/{requestId}/inspection-date` with `scheduledAt` and an optional `note`. Allowed while the job is `assigned` or `task_accepted`; after the work started it is refused with `409`. The change is written to the request history.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.StartInspection))] = new(5, "Queue 5 · Inspector: start the inspection",
            """
            **When:** the inspector arrives and begins. **Who:** the assigned inspector (the technical manager and Admin can too).
            **How:** `POST /quotation-queue/{requestId}/start-inspection`, no body. It accepts the task if needed and moves the job to `in_progress`; calling it again is safe. The answer is the list of machines to inspect (same as queue 6).
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.ListMachines))] = new(6, "Queue 6 · List the machines of a job",
            """
            **How:** `GET /quotation-queue/{requestId}/machines`. One row per machine of the job: `requestItemId`, name, quantity, `templateKey`, the certificate `status` (`not_started`, `draft`, `submitted`, `returned`, `approved`, `delivered`), how many checklist rows are answered out of `checklistTotal`, the review note when it was returned, and the certificate number when approved. `progress` counts the machines submitted, approved and delivered. Everyone who can see the job can call it.
            **Buttons:** `canStartInspection` (show "Start inspection"), `canDeliver` (show "Deliver to client"), `canReview` (the job waits for the technical manager: show **reject**) and `canApprove` (it can be approved now: show **accept**) and, per machine, `canEdit` (inspector can fill it) already take the current user and the job's state into account, so the app does not have to work it out. When `canReview` is true and `canApprove` is false, **`reviewBlockers`** lists why (for example `Forklift Truck: the inspector has not filled in its certificate`): show it instead of the accept button; the job can still be sent back. `requestItemId` is the `id` of the item in `GET /requests/{requestId}`.
            **The review screen** needs no other call: every machine row also carries what is printed on its certificate (`type`, `templateName`, `manufacturer`, `capacity` (SWL), `model`, `inspectionStandards`, `examinationDate`, `nextExaminationDate`), the `result`, `pdfUrl` (the certificate as it will be printed, DRAFT until approved) and `qrUrl` (what its QR code opens); the job has `previewZipUrl` for all the certificates in one ZIP and `reviewHistory` for the earlier comments.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.OpenMachine))] = new(7, "Queue 7 · Open one machine (the whole form in one call)",
            """
            **How:** `GET /quotation-queue/{requestId}/machines/{requestItemId}`. It returns everything the screen needs:
            - `header`: client, address, representative, the inspected item, the standard(s) and the last/next examination dates.
            - `fields`: the technical fields of this kind of machine, already filled with the machine's own details where known (manufacturer, SWL, serial...).
            - `checklist`: sections and rows (`key` is `section.item`) with the saved answer, or empty when the machine has no checklist. `checklistValues` are `yes`, `no`, `na`.
            - `answers`, `texts`, `dates` (as `yyyy-MM-dd`): what was saved of the questionnaire; **`result`** is `satisfactory`, `unsatisfactory` or empty; `reviewNote` is the technical manager's comment and note after the job was sent back.
            - `canEdit`, `canReview` and `requestStatus` say what the current user can do now. **`readyToSubmit`** is true when nothing is missing, and **`missing`** lists what still stops a submit (for example `the examination date`, `12 checklist row(s)`), so the Submit button can be enabled and explained before it is pressed.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.SaveMachine))] = new(8, "Queue 8 · Inspector: save the machine (as often as needed)",
            """
            **How:** `PUT /quotation-queue/{requestId}/machines/{requestItemId}`. Send only what changed; everything is optional and merged with what was saved.
            - `fields`: `{ "ndt": "MT, no cracks" }` (keys from `fields` of queue 7).
            - `checklist`: `{ "tires_and_wheels.sidewalls_cracks": { "value": "no", "note": "Cut on the tyre" } }`. The value is `yes`, `no` or `na`; a `no` needs a note.
            - **`result`**: `satisfactory` or `unsatisfactory` (the checkbox at the end of the form). **Required before submitting**; Conclusion/Remarks on the certificate shows that one word only, Satisfactory or Unsatisfactory (the `conclusion` text is not printed). An unsatisfactory machine also needs `defectDescription`.
            - `answers`: `firstExamination`, `installedCorrectly`, `within6Months`, `within12Months` (the other one is filled in as the opposite), `examinationScheme`, `exceptionalCircumstances`, `existingDanger`, `futureDanger`.
            - `texts`: `testType`, `conclusion`, `defectDescription`, `repairsRequired`, `testsCarriedOut`, `note`, `clientRepresentative`. `dates`: `examination`, `nextExamination`, `futureDangerBy`.
            Unknown keys answer `400`. Only while the job is `in_progress` (or returned); a submitted certificate is locked (`409`). The answer is the same form as queue 7, so the screen can refresh from it.
            Example (everything optional, dates are `yyyy-MM-dd`):
            ```json
            {
              "fields": { "ndt": "MT, no cracks", "loadTest": "110% of SWL" },
              "checklist": {
                "tires_and_wheels.tread_tread_mounting": { "value": "yes" },
                "tires_and_wheels.sidewalls_cracks": { "value": "no", "note": "Cut on the tyre" }
              },
              "answers": { "firstExamination": false, "within12Months": true, "examinationScheme": true, "exceptionalCircumstances": false, "existingDanger": false, "futureDanger": false },
              "result": "unsatisfactory",
              "texts": { "testType": "Visual", "defectDescription": "Cut tyre", "repairsRequired": "Replace the tyre" },
              "dates": { "examination": "2026-11-05", "nextExamination": "2027-11-05" }
            }
            ```
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.SubmitMachine))] = new(9, "Queue 9 · Inspector: submit the machine for review",
            """
            **How:** `POST /quotation-queue/{requestId}/machines/{requestItemId}/submit`, no body. If something is missing the answer is `409` with `code: certificate_incomplete` and a **`problems` array** with one entry per missing item (the result, dates, questionnaire, unanswered checklist rows, notes for `no`, the defect description for an unsatisfactory machine) that the app can show as a list. Blank texts get the usual wording (`NONE`, `N/A`).
            The first submit gives the certificate its **QR code** (kept from then on), so the previews in queues 10 and 10b show it. Every machine needs a certificate, Satisfactory or Unsatisfactory: when **every machine** of the job is submitted, the job moves to `certificate_submitted` and appears for the technical manager in `GET /quotation-queue/to-review`.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.DownloadMachinePdf))] = new(10, "Queue 10 · Inspector and technical manager: see the certificate (PDF)",
            """
            **When:** any time after the machine was saved, to see exactly how the certificate will look. **Who:** the inspector of the job, the technical manager and Admin; the coordinator once the job is approved (`409` before).
            **How:** `GET /quotation-queue/{requestId}/machines/{requestItemId}/pdf`. Before approval it is marked **DRAFT**, carries the **QR code** from the first submit and shows `(on approval)` for the number; after approval it is the **stored final file**, byte for byte as issued.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.PreviewCertificatesZip))] = new(10.5, "Queue 10b · Technical manager: preview all the job's certificates (ZIP)",
            """
            **When:** before deciding, to look at every certificate of the job at once with its QR and everything. **Who:** the technical manager, Admin and the job's inspector; the coordinator gets `403` and uses queue 17 after approval.
            **How:** `GET /quotation-queue/{requestId}/certificates/preview.zip`. One PDF per machine that has been saved (`01-forklift-truck.pdf`, ...), exactly as it will be printed: marked **DRAFT** until approved, the stored final file after. Machines not saved yet are left out; `404` when none is saved.
            """),
        [Key<QuotationQueueController>(nameof(QuotationQueueController.ListToReview))] = new(11, "Queue 11 · Technical manager: the jobs to review",
            """
            **Who:** the technical manager or Admin. **How:** `GET /quotation-queue/to-review` (the same as `GET /quotation-queue?status=review`) lists the jobs whose machines are all submitted (request status `certificate_submitted`), with `certificates` counting the machines. Open one with queue 6 (`canReview` is true on the job and its machines) and the certificates with queue 10b.
            The other parts of `certify` work the same way: `status=returned` (the inspector's jobs sent back to fix), `status=approved` (the coordinator's jobs ready to hand over), `status=delivered`.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.ApproveAll))] = new(12, "Queue 12 · Technical manager: approve the job",
            """
            **When:** the job is `certificate_submitted` (every machine submitted) and the previews are right. The job is approved **as a whole**, never machine by machine, because its certificates go to the client together.
            **Who:** the technical manager or Admin. **How:** `POST /quotation-queue/{requestId}/approve-all`. The body is optional: `{ "signature": "[[0.1,0.2,0.3,0.4]]", "rememberSignature": true }` where `signature` is the hand-drawn signature as a JSON array of strokes (each stroke is x,y numbers between 0 and 1). Without a `signature` the one saved earlier is used, and without any the certificate carries the name only.
            Every certificate, **Satisfactory or Unsatisfactory**, gets its number (**`CERT-{year}-{0001...}`**, counting per team and year) and keeps the QR made on submit; the **final PDF is drawn once and filed under the client** (see the client's certificates in Configurations), and the machines' examination dates and result are written to their records. The job becomes `certificate_approved` and **goes to the coordinator** that owns it (`status=approved`), with the certificates and their QR codes (queues 15-18). A job that is not ready answers `409` with `code: job_not_ready` and a **`problems`** list, one line per machine that has no submitted certificate, and **nothing is changed**; a signature that is not an array of strokes (an image, for example) answers `400` with the expected format. The approval is **all or nothing**: it is saved in one transaction, so if anything fails no certificate is approved and no number is used up.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.ReturnToInspector))] = new(14, "Queue 14 · Technical manager: send the job back to the inspector",
            """
            **When:** something in the certificates must be fixed (a wrong value, a spelling mistake...). The whole job goes back.
            **How:** `POST /quotation-queue/{requestId}/return-to-inspector` with a general `note` (required) and optional comments on single machines:
            ```json
            { "note": "Please fix and resubmit",
              "machines": [ { "requestItemId": "...", "comment": "Serial number spelled wrong" } ] }
            ```
            Every submitted machine is reopened (a job that reached review with machines that were never submitted can be sent back too, so the inspector can finish them). Each shows its own comment and the note in `reviewNote` (queues 6 and 7), and every comment is kept in the job's **`reviewHistory`** (queue 6), newest first. The job becomes `certificate_returned` and shows for the inspector in `status=returned`; the inspector fixes the machines (queue 8) and submits them all again (queue 9), and the job is back in `to-review`.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.DownloadMachineQrCode))] = new(15, "Queue 15 · Download a certificate's QR code (PNG)",
            """
            **When:** the machine is approved. **How:** `GET /quotation-queue/{requestId}/machines/{requestItemId}/qr.png`. The same QR that is printed on the approved certificate, as an image to send to the client. Scanning it opens the **public check page** (no sign-in): it confirms the certificate is genuine (valid, or expired after its next examination date) and has a **Download certificate (PDF)** button that gives the certificate exactly as it was issued (`GET /verify/{code}/pdf`).
            The same QR stays with the certificate in the client's archive (`GET /clients/{clientId}/certificates`, field `qrUrl`, and `.../{id}/qr.png`), so it can be sent again at any time.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.PreviewDelivery))] = new(16, "Queue 16 · Coordinator: preview the email to the client",
            """
            **When:** the job is `certificate_approved` (`status=approved`). **How:** `GET /quotation-queue/{requestId}/deliver-to-client/preview`. It returns who the email would go to (**the sender of the original request first**, then the client's certificates email), the subject (`Re: ...`, so it stays in the same thread), the suggested text, the attachment names, and `emailEnabled`. When email delivery is switched off `notice` says so and nothing is sent later.
            """),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.DownloadCertificatesZip))] = new(17, "Queue 17 · Coordinator: download all certificates and QR codes (ZIP)",
            "**How:** `GET /quotation-queue/{requestId}/certificates.zip` returns every certificate PDF of the job and, under `qr/`, the QR code of each one, for the coordinator, technical manager and Admin once the job is approved."),
        [Key<MachineInspectionController>(nameof(MachineInspectionController.DeliverToClient))] = new(18, "Queue 18 · Coordinator: hand the certificates to the client",
            """
            **When:** the job is `certificate_approved` (every machine approved).
            **How:** `POST /quotation-queue/{requestId}/deliver-to-client`, optional body `{ "sentTo": "someone@client.com", "message": "text of the email" }`. `sentTo` overrides the recipient from the preview and `message` replaces the suggested text.
            - **Email delivery on:** the certificates are emailed as a reply to the original request, from the coordinator's mailbox, with every PDF attached. If the mail server refuses, the answer is `502` and nothing changes, so it can be repeated (never two emails).
            - **Email delivery off (the default for now):** nothing is emailed and nothing fails; the certificates are just marked as handed over.
            Either way the certificates become `delivered` and the job `sent_to_client` (`status=delivered`). The answer has `delivery: { emailSent, sentTo, notice }`. **Who:** the owning coordinator, the technical manager or Admin.
            """),
    };

    internal static string QueueDescription =>
        """
        Where an accepted quotation goes next. **One list endpoint, `GET /quotation-queue?status=...`**, returns them by queue; each row carries `queue` (`unassigned`, `assigned` or `certify`) and `requestStatus`.

        | `status` | Meaning | Who acts next |
        |---|---|---|
        | `unassigned` | Accepted, waiting for an inspector | Coordinator |
        | `assigned` | Given to an inspector, inspection not finished | Inspector |
        | `certify` | Inspection done, certificate on its way (all four below) | — |
        | `review` | Every machine submitted | Technical manager |
        | `returned` | Sent back to fix | Inspector |
        | `approved` | Every certificate approved, with its QR code | Coordinator |
        | `delivered` | Handed to the client | — |

        1. **List** the queue you need. 2. **Pick an inspector** (`GET /inspectors`) and 3. **assign** the job (`POST /requests/{id}/assignment`). The coordinator that owns the quotation keeps full access and can hand the job to another inspector until the work starts.
        The inspector sees only their own jobs, without prices, and can **change the inspection date** (4), nothing else.

        **Inspector (steps 5-10):** 5. **start** the inspection; 6. see the **machines**; 7. **open** one (the form of that kind of machine); 8. **save** it as often as needed; 8. choose the **result** (satisfactory or unsatisfactory) for every machine; 9. **submit** it (this makes its QR); 10. see the **certificate PDF** (marked DRAFT, with the QR) at any time. When every machine is submitted the job goes to the technical manager (`to-review`).

        **Technical manager (steps 10-14), always on the whole job:** 11. list the jobs **to review** (`GET /quotation-queue/to-review`); 10b. **preview all its certificates** with their QR codes in one ZIP (or 10. one at a time); then either 12. **approve the job**: every certificate, Satisfactory or Unsatisfactory, gets its number and final PDF, filed under the client, and the job goes to the coordinator (`status=approved`); or 14. **send the job back** to the inspector with a note and comments per machine: it shows for the inspector in `status=returned`, who fixes and submits everything again. Every comment stays in the job's `reviewHistory`.

        **Coordinator (steps 15-18):** 15. the **QR code** of each certificate (printed on it once approved; scanning it opens the public check page with the certificate's PDF); 16. **preview** the email, 17. download the **ZIP** (certificates and QR codes) and 18. **hand the certificates to the client**. Every certificate stays in the client's archive with its QR code (Configurations: the client's certificates).

        Each machine's certificate status is `not_started`, `draft`, `submitted`, `returned`, `approved` or `delivered`.
        """;

    /// <summary>Admin-only client management, kept apart for testing.</summary>
    public const string TalabatTag = "talabat 3rfa";

    private static readonly Dictionary<string, Step> TalabatSteps = new()
    {
        [Key<AdminClientsController>(nameof(AdminClientsController.Create))] = new(1, "Admin 1 · Add a client",
            """
            **Who:** Admin only. **How:** `POST /admin/clients` with the client fields and the `contacts` (same body as setup 1) plus **`managerId`**, the technical manager whose team the client belongs to (required).
            """),
        [Key<AdminClientsController>(nameof(AdminClientsController.List))] = new(2, "Admin 2 · List the clients of every team",
            "**How:** `GET /admin/clients` returns the clients of all teams by name; `includeArchived=true` also returns archived ones."),
        [Key<AdminClientsController>(nameof(AdminClientsController.Get))] = new(3, "Admin 3 · Open a client",
            "**How:** `GET /admin/clients/{clientId}` returns the client with its contacts."),
        [Key<AdminClientsController>(nameof(AdminClientsController.Update))] = new(4, "Admin 4 · Change a client and its contacts",
            "**How:** `PUT /admin/clients/{clientId}` with the profile fields. Add `contacts` to change them: a contact with an `id` is edited, one without is added, one left out is removed. Leave `contacts` out to keep them."),
        [Key<AdminClientsController>(nameof(AdminClientsController.Delete))] = new(5, "Admin 5 · Archive or delete a client",
            """
            **How:** `DELETE /admin/clients/{clientId}`.
            - Default (`permanent=false`): the client is **archived**: it leaves the lists and can no longer be edited.
            - `permanent=true`: the client and its contacts are **deleted for good**. Refused with `409 client_in_use` while requests or registered items still use the client; archive it instead.
            """),
    };

    internal static string TalabatDescription =>
        """
        Admin-only shortcuts for managing clients of any team while testing: add (with the team's `managerId`), list, open, change, and archive or delete. Technical managers keep using the same actions for their own team under **Configurations**.
        """;

    /// <summary>The setup section: clients with their contacts and the items registered for each client.</summary>
    public const string ConfigTag = "Configurations";

    private static readonly Dictionary<string, Step> ConfigSteps = new()
    {
        [Key<ClientsController>(nameof(ClientsController.Create))] = new(1, "Setup 1 · Create a client",
            """
            **When:** a new customer is set up, before registering its items.
            **How:** send `name`, `isCompany`, `hasContract`, `location` (with optional `latitude`/`longitude`), `certificatesEmail` (where certificates are sent), `taxNumber`, `notes` and the `contacts` (name, email, phone, one `isPrimary`). **Who:** Technical managers and Admin only (a coordinator gets `403`). A manager creates the client for their own team; Admin sends `managerId` to choose the team.
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
            **When:** a client has equipment to be inspected. Every item belongs to one client, and the client must exist and not be archived. **Who:** Technical managers and Admin only.
            **How:** send `clientId`, `name` and, optionally, `itemType`, `category`, `manufacturer`, `model`, `serialNumber`, `capacity`, `locationInsideClientLocation`, `notes`, `labels`, `dates`, `checklist`, `attachments` and **`inspectionStandard`**, the standard the item is inspected against (for example `ASME B30.5`), and **`testType`**, how it is tested (for example `Visual & function test`; optional, printed on its certificates, and the inspector can change it there).
            **Note:** items are also registered automatically from the machines of a saved request. Select one in a request with its `equipmentId`.
            """),
        [Key<EquipmentController>(nameof(EquipmentController.List))] = new(6, "Setup 6 · List items",
            "**How:** `GET /equipment?clientId=...` returns the items of one client (or of all your clients without `clientId`). `includeArchived=true` also returns archived ones."),
        [Key<EquipmentController>(nameof(EquipmentController.GetById))] = new(7, "Setup 7 · Open an item",
            "**How:** `GET /equipment/{equipmentId}` returns the item with its `inspectionStandard`, dates and checklist."),
        [Key<EquipmentController>(nameof(EquipmentController.Update))] = new(8, "Setup 8 · Update an item",
            """
            **How:** `PUT /equipment/{equipmentId}` with the same body as the creation. `clientId` must stay the same: an item cannot move to another client. `inspectionStandard` and `testType` are kept when left out, changed when sent, and cleared when sent as an empty text.
            """),
        [Key<ClientCertificatesController>(nameof(ClientCertificatesController.List))] = new(19, "Extra · Certificates issued for a client",
            "**How:** `GET /clients/{clientId}/certificates` lists the certificates issued for the client, newest first (number, item, examination and next examination dates, SHA-256 of the PDF). A certificate is filed here when the technical manager approves it."),
        [Key<ClientCertificatesController>(nameof(ClientCertificatesController.DownloadPdf))] = new(19.5, "Extra · Download an issued certificate",
            "**How:** `GET /clients/{clientId}/certificates/{certificateFileId}/pdf` returns the PDF exactly as it was issued."),
        [Key<ClientCertificatesController>(nameof(ClientCertificatesController.DownloadQrCode))] = new(19.6, "Extra · Download an issued certificate's QR code",
            "**How:** `GET /clients/{clientId}/certificates/{certificateFileId}/qr.png` returns the certificate's QR code (the one printed on it) as an image to send to the client; it opens the public check page with the PDF download (`qrUrl` in the list)."),
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
        One-time setup of the data the workflow uses, in the order to do it. **Creating, updating and archiving clients and items is for Technical managers and Admin;** coordinators can list and open them (to pick a client in a request).

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

        After acceptance the quotation continues in the **Quotation queue** section: unassigned, assigned to an inspector, then certification.

        Amounts are in piastres (1 EGP = 100 piastres). Statuses and enums are lowercase snake_case in JSON.
        Email requests can only be changed by the coordinator whose mailbox received them; managers and admins can read them.
        """;

    private static string Key<TController>(string action) => $"{typeof(TController).Name}.{action}";

    internal static bool TryGetStep(OperationFilterContext context, out double order, out string title, out string guide, out string tag)
    {
        var type = context.MethodInfo.DeclaringType?.Name;
        var key = $"{type}.{context.MethodInfo.Name}";
        if (type is not null &&
            (Steps.TryGetValue(key, out var step) || QueueSteps.TryGetValue(key, out step) || ConfigSteps.TryGetValue(key, out step) || TalabatSteps.TryGetValue(key, out step)))
        {
            (order, title, guide) = (step.Order, step.Title, step.Guide);
            tag = Steps.ContainsKey(key) ? Tag : QueueSteps.ContainsKey(key) ? QueueTag : TalabatSteps.ContainsKey(key) ? TalabatTag : ConfigTag;
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
            .OrderBy(name => name == WorkflowDocs.Tag ? 0
                : name == WorkflowDocs.QueueTag ? 1
                : name == WorkflowDocs.ConfigTag ? 2
                : name == WorkflowDocs.TalabatTag ? 3
                : WorkflowDocs.LegacyTags.ContainsKey(name) ? 5 : 4)
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
                    : name == WorkflowDocs.QueueTag
                    ? WorkflowDocs.QueueDescription
                    : name == WorkflowDocs.TalabatTag
                    ? WorkflowDocs.TalabatDescription
                    : name == WorkflowDocs.Tag
                    ? WorkflowDocs.Description
                    : WorkflowDocs.LegacyTags.GetValueOrDefault(name) ?? described.GetValueOrDefault(name)
            })
            .ToList();
    }
}
