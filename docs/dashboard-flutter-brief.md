# Inspecta management dashboard: brief for the Flutter build

A web dashboard for Integrity management, to sit beside the Inspecta mobile app and read and write the **same Firebase project** (`inspecta-68798`). An HTML version and a first Flutter scaffold were built and then deleted; this file keeps what they learned so the new Flutter version doesn't start from zero. Nothing here is code. Colours and layout are placeholders until the new design lands.

## 1. Goal

Management view of the whole system, with insights:

1. **Clients**: create them, with their details, contacts, equipment, and every certificate delivered to them.
2. **Inspectors**: create them, with their records (certificates, qualifications, status).
3. **Timesheets**: per inspector, with approval.
4. **Overview**: what needs attention and how the work is moving.

## 2. Recommended shape

- **A separate Flutter web project** (for example `dashboard_app/` in this repo, its own `pubspec.yaml`), not a second entry point in the mobile app. Reasons: its own small dependency set and web build (no QR scanner, camera or PDF packages), its own deploy, no risk to the mobile app.
- **Cost of that choice:** it can't reuse the mobile app's Dart models, so write small Firestore parsers (section 4) and keep them in step with `lib/features/*/data/models/` by hand.
- **Same Firebase project:** reuse the web options from `lib/firebase_options.dart`. Add no new Firebase setup.
- **Suggested stack:** `firebase_core`, `firebase_auth`, `cloud_firestore` (versions as in the mobile app's `pubspec.yaml`), `flutter_bloc`, `equatable`, `intl`, `go_router` (real URLs, redirects on sign-in state), path URL strategy.
- **Suggested layout:** `core/` (theme, router, widgets), `features/auth`, `features/shell`, then one folder per section, each split data / domain / presentation like the mobile app.
- **Two bugs the first scaffold hit, worth remembering:**
  - A shell page built before the session is known must rebuild when it arrives (on a browser reload no navigation happens afterwards). Use a `BlocBuilder` on the session inside the shell.
  - Never wipe a form with a live re-render when snapshots update; skip rebuilding forms while someone is typing.

## 3. Sign-in and roles

Same email/password accounts as the app. After sign-in, read `users/{uid}`:

- no document, or `active != true` → "No access" screen, offer sign-out.
- `role` read as: `admin`, `coordinator` (the old stored value `supervisor` also means coordinator), `technical_manager`, `inspector`.
- **inspectors never get in.**

| Role | Sees | Can do |
|---|---|---|
| admin | everything | create/edit clients and equipment, add inspectors, edit qualifications, deactivate/reactivate inspectors, approve/return timesheets |
| coordinator | clients, equipment, certificates, inspectors, timesheets | create/edit clients and equipment, approve/return timesheets |
| technical_manager | clients, equipment, certificates, inspectors | read only; **no timesheets** (the rules give them none) |

Hide what a role can't do, but `firestore.rules` is the real gate. Treat a profile change as live: watch the `users/{uid}` document, so deactivating someone signs them out of the dashboard.

**Rules change already deployed to `inspecta-68798`** (in the working tree of `firestore.rules`, not committed): admins may create/update `clients` and approve/return `timesheets`. Before, only coordinators could. Keep it if admins use the dashboard.

## 4. Data map (Firestore)

All dates are Firestore `Timestamp`s. Amounts are **piastres** (EGP × 100).

**`clients/{id}`**: `companyName`, `location`, `certificatesEmail`, `createdAt`, `contacts[]` = `{name, role, email, phone, isMain}`, `equipment[]` = `{id, type, serialNumber, location, lastInspectionResult, nextDueDate}`. Equipment lives inside the client document. Give each item a stable `id` (a fresh Firestore doc id) so it can be removed; removing an item needs a transaction that filters the array. Adding can use `arrayUnion`.

**`users/{uid}`**: `name`, `email`, `phone`, `role`, `active`, `qualifications[]` = `{name, validUntil}` (very old data may be plain strings), `signatureImageId`, `lastLogin`, `onLeaveUntil`. Inspectors are the docs with `role == 'inspector'` (one query serves admin, coordinator and technical manager).

**`requests/{id}`** (the jobs): `clientId`, `clientName`, `status`, `items[]` = `{id, type, serialNumber, manufacturer, model, capacity, quantity, ...}`, `inspectorId`, `scheduledAt`, `receivedAt`, `certificateSubmittedAt`, `location`. Read all of them for the pipeline and to join certificates and timesheets to a client.

**`certificates/{requestId}`** (doc id equals the job id): `inspectorId`, `finalResult` (`safe_to_operate` | `safe_with_conditions` | `not_safe`), `texts{}` (`serialNumber`, `standardOfInspection`, `preparedByName`, ...), `dates{}` (`examination`, `nextExamination`), `submittedAt`, `reviewedAt`, `reviewerName`, `sentTo` (set when the coordinator sends it). **Issued when the technical manager approves** (all three written together, absent before): `certificateNumber` (the real `CERT-year-nnnn`), `verificationCode` (e.g. `K7QM-2XPA-9WDF`) and `qrData` — the exact text the certificate PDF's QR code carries. Draw the QR from `qrData` so the dashboard and the PDF show the same code. Its stage comes from the job's status:

| Job `status` | Certificate stage |
|---|---|
| `certificate_submitted` | In review |
| `certificate_returned` | Returned |
| `certificate_approved` | Approved by the technical manager, with the coordinator to send (QR is issued) |
| `sent_to_client` | Sent to client by the coordinator |
| anything else | still a draft: don't list it |

**`timesheets/{requestId}`** (doc id equals the job id): `inspectorId`, minutes in `inspectionMinutes`, `reportMinutes`, `waitingMinutes`, `unpaidBreakMinutes` (net paid time = the first three; `minutes` is the stored net, and very old entries have only `minutes`), `hourlyRatePiastres`, `pricePiastres`, `internalTransportPiastres`, `externalTransportPiastres`, `status` (`pending` | `approved` | `returned`), `reviewNote`, `reviewedBy`, `reviewerName`, `reviewedAt`, `updatedAt`. **There is no date or client on an entry**: join to its request (`scheduledAt`, then `certificateSubmittedAt`, then `receivedAt`) for the date, client and job.

**`timesheetSettings/{uid}`**: `hourlyRatePiastres`. Readable by admin only; coordinators fall back to the rate stored on the inspector's latest timesheet entry.

**`requests/{id}/events`**: append-only status history; not needed for the first version.

## 5. Screens

1. **Overview**
   - *Needs your attention*: equipment past its due date; certificates waiting for review; timesheets waiting for approval (roles that can see them); inspector qualifications expired or expiring within 45 days.
   - Four key figures: jobs in flight, certificates this month, equipment due in 30 days, timesheets to approve.
   - Certificates issued per month, stacked by result, last 7 months (approved or sent only, by `reviewedAt`).
   - Job pipeline from `requests.status`, grouped: Request received · Quote sent (draft + sent) · Client countered · Assigned (quote_accepted + assigned + task_accepted) · In progress · Certificate submitted (submitted + returned) · Sent to client (approved + sent).
   - Equipment coming due (overdue first, then 30 days) and inspector workload (paid hours, last 30 days).
2. **Clients**: searchable list (company, main contact, equipment count, due-date state, certificates, last certificate). **Client page**: facts strip, tabs Equipment / Certificates / Contacts; add and remove equipment; edit client details and contacts. **New client form**: company, location, certificates email, contacts, optional equipment.
3. **Equipment**: all items across clients with filters All / Overdue / Due in 30 days / Later, sorted by next due.
4. **Certificates**: results mix (safe / with conditions / not safe), tabs by stage, search by reference, client, serial or inspector. Show the stored `certificateNumber` (certificates approved before the QR was introduced have none; fall back to a short job reference such as `JOB-` plus the first six characters of the job id). The QR comes from `qrData`, same fallback.
5. **Inspectors**: list with status (Active / On leave until / Inactive), worst qualification state, certificates, hours and pending timesheets. **Inspector page**: facts, tabs Record / Qualifications (admin can add and remove) / Timesheets; activate and deactivate (ask twice before deactivating). **Add inspector form**: name, email, phone, qualifications.
6. **Timesheets** (not for technical managers): inspector picker with pending counts; period This week / Last 4 weeks / All time; totals (paid time, labour, transport, awaiting approval); a Sunday-to-Saturday week strip per inspector (Friday and Saturday are the weekend); entries with a time-split bar; Approve, Return (a reason is required) and Approve all pending.

## 6. Rules the writes must respect

- **Approve/return a timesheet** touches only `status`, `reviewNote`, `reviewedBy`, `reviewerName`, `reviewedAt`: `reviewedBy` = the signed-in uid, `reviewedAt` = server timestamp. Only a `pending` entry can be decided. Approval must **not** carry a `reviewNote` (delete the field); a return needs a non-empty one. (The mobile app no longer has a timesheet-review screen; this is the contract the rules enforce.)
- **Clients**: create/update by coordinator or admin; no delete.
- **Creating an inspector** needs a sign-in account plus a profile:
  - Creating a Firebase Auth user signs that user in, so do it on a **throwaway second `FirebaseApp`** (initialize it with the main app's options, create the user, then sign out and delete that app). The admin stays signed in on the main app.
  - Then write `users/{uid}` with `role: 'inspector'`, `active: true` (only an admin may create or delete user documents).
  - If the profile write fails, delete the just-created account so no sign-in is left without a profile.
  - Use a random password the person never sees and send a password-reset email so they set their own.
- **The hourly rate can only be set by the inspector** in the app (`timesheetSettings`), so the add-inspector form must not ask for one.
- Coordinators may only change an inspector's `onLeaveUntil`; admins may update any field.
- Request queries: the mobile app's rules let coordinator, technical manager and admin read every request and certificate; an inspector sees only their own.

## 7. Design direction

The look was meant to follow the Integrity logo (`assets/pngs/integraty-logo.png`): deep navy structure (about `#183C78`), mid-blue for everything interactive (about `#3084C0`), signal green only for safe / approved / verified (about `#0C8430` to `#48C054`), amber and red only for verdicts and due dates. A navy sidebar with light content, Inter (Latin) and Cairo (Arabic) fonts, tables and filters for density. These are a starting point; the new design replaces them.

## 8. Quality bars to keep

- Never colour alone for status: chips carry text.
- Real empty, loading and error states for every list; a write shows a busy state and an honest error message.
- A destructive action (deactivate, remove equipment) asks twice.
- Desktop first; below about 900px the sidebar becomes a drawer. Use logical start/end alignment so Arabic and right-to-left can follow.
- English only at first; the mobile app already has Arabic.

## 9. Hosting and testing

- **Hosting:** Firebase Hosting site `inspecta-manage` (`https://inspecta-manage.web.app`) exists in `inspecta-68798` but is **disabled**; deploying re-enables it. `firebase.json` currently has a single `hosting` entry for `website/` on the default site, so a second site needs `hosting` turned into an array keyed by `site`, with `"public": "<project>/build/web"` and an SPA rewrite `** → /index.html`. Add `X-Robots-Tag: noindex` and `X-Frame-Options: DENY`.
- **Verification used before:** headless Chrome over CDP for screenshots, plus Flutter widget tests that drive the router with a fake auth repository (signed out, admin, technical manager, denied). Remember `MSYS_NO_PATHCONV=1` in Git Bash when passing `#/...` routes.
- **Live data checks need a person to sign in** with a real account; scripts reusing remembered test-account passwords were blocked.
- **Don't** run `flutter analyze` as the check in this project (standing user preference); build and run instead.

## 10. Not decided yet

- PDF download of a certificate from the dashboard.
- Marking an inspector on leave, and editing an equipment item's due date.
- Whether the dashboard should show the approved-by name.
- Arabic and right-to-left.
- Global search across clients, equipment, certificates and inspectors.
