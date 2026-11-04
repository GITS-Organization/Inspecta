# Integrity web app

Angular web client of Integrity (integrity.gitsplatform.com) for the Inspecta backend. Standalone components, signals,
strict TypeScript, SCSS, no NgModules. Design: `docs/frontend-design/integrity-app.html`. Plan: [PLAN.md](PLAN.md).

## Run

```bash
npm install
npm start
```

`npm start` serves http://localhost:4200 and proxies `/api` to `https://api.gitsplatform.com` (see `proxy.conf.json`), so there is
no CORS setup. Sign in with an account that exists on that backend. To use a local backend change the `target` in `proxy.conf.json`.

### Run it locally against the real API

Every screen calls the real `/api/v1` through that proxy: there is no mock, in-memory or demo data in the dev or production build
(mocked `HttpClient` exists only inside the unit tests). The backend must be the version that has Man day, job machines, gear
division, the catalogue names with kind and standards, the certificate answer defaults, assigning without a quotation, the mailbox
import date and the `from`/`to` date filters (all on the `backend` branch).

1. `cd web/integrity && npm install`
2. `npm start` (port 4200; `npm start -- --port 4202` if that is taken).
3. Open the printed address and sign in with **your own test accounts**, one per role (admin, technical manager, coordinator,
   inspector) created on that backend. Never put credentials in the repository, in `proxy.conf.json` or in a test.

### Manual test checklist

Tick each line on a real (test) team. Anything that fails shows the API's own sentence on screen: note it with the screen.

- **Login, every role:** signs in and lands on its start page (admin / technical manager: Overview; coordinator: Requests; inspector:
  My jobs); a wrong password shows a plain message; Sign out returns to the login page.
- **Date filter (Requests, Quotations, Jobs, To review, Overview):** the bar at the top; each preset (Today, Last 7 days, This month,
  Last month, Since 1 Nov 2026) changes the lists and, on Requests, the tab counts; Custom with an end before the start is refused;
  `?from=&to=` stays in the address after a refresh; Clear brings everything back; an empty list says the dates are the reason and
  offers Clear dates; Clients has no date bar.
- **Coordinator, requests:** the tabs (New, Needs details, Accepted, Man days, Closed, Unknown senders, All) and the search box work
  together with the dates; a request is opened, completed (client, location, items) and its quotation prepared.
- **Coordinator, quotation:** prices, send, client answer; a Man day / Contract / Direct job never shows a price or a quotation link.
- **Coordinator, new man day:** *New man day* on Requests or Jobs: search a registered client, choose an inspector (on-leave is
  flagged; the API refuses them), date and time, location prefilled; the job opens with "Add the first machine".
- **Coordinator, contract client:** on a new request of a client that has a contract, with client and location filled in,
  *Assign an inspector (contract client)* appears (list row and request screen); it says no quotation will be sent; after confirming
  the job is assigned with the **Contract** tag; a client without a contract never shows the action.
- **Item names:** in the request items, the add-a-machine dialog, client items and Create certification the name is a searchable
  select of the catalogue; the kind and the standards appear under it ("No standard defined yet" for Container, STS crane, Trailer
  Truck); an old name that is not in the list shows "Not in the list: choose a name"; Quantity shows only for gears, Serial number
  only for the others; there is no "Where on site".
- **Inspector, job:** the job opens from My jobs; *Add a machine* (a registered one, a new one: it appears in the client's items);
  edit and remove a machine (the API's refusal is shown for a started or submitted one); *Start inspection*.
- **Inspector, gear division:** on a gear machine *Divide into certificates*: choose how many (no maximum), each certificate has a Qty
  and one Serial numbers box with a live counter against 1000; typing or pasting past it is cut with a message; a differing total only
  warns; save, reopen and change the number; once a certificate is started the group is read-only.
- **Inspector, certificate form:** the eight answers start filled; the two linked pairs (first examination / installed correctly,
  within 6 months / within 12 months) flip each other; for a gear the serial text and the four texts share one counter of 1200
  characters and typing past it is blocked; save a draft and send to the technical manager.
- **Technical manager:** Overview (with dates), To review (the job opens, a certificate preview shows, approve all, or return with a
  note); Mailbox (below).
- **Mailbox date (technical manager, admin):** each coordinator shows its mailbox and the date mail is imported from (default 1 Nov
  2026); the date picker takes 2020 up to 30 days ahead; the explanation and the confirmation come before saving; the new date shows.
- **Phone width:** the date control is a chip that opens a sheet; dialogs fit; nothing scrolls sideways.

## Test and build

```bash
npm test -- --watch=false
npm run build
```

The production build (`dist/integrity/browser`) swaps `src/environments/environment.ts` for `environment.production.ts`, which calls
`https://api.gitsplatform.com/api/v1` directly. Change `apiBase` there if the API moves.

## Deploy (Firebase Hosting)

The app is a single-page app served at `integrity.gitsplatform.com`. `firebase.json` points the `integrity` hosting site at
`dist/integrity/browser` and rewrites every path to `index.html`.

```bash
npm run build
firebase deploy --only hosting:integrity
```

Needs a one-time `firebase use <project>` and the `integrity` site created in that project (`firebase hosting:sites:create integrity`),
with the custom domain connected in the console. The API must allow the origin `https://integrity.gitsplatform.com` in its CORS settings.

## What is where

| Folder | Content |
|---|---|
| `src/app/core` | models, `AuthService` (JWT + refresh rotation, role from the `role` claim), interceptor, guards, branding |
| `src/app/api` | typed endpoint wrappers; `certification.api.ts` is the Create certification contract |
| `src/app/shared` | the certificate "paper" preview, formatting, serial-number split logic |
| `src/app/shell` | navigation rail per role |
| `src/app/features` | login, overview, jobs, job, review, machine-form, create |

Roles and start pages: admin and technical manager open **Overview** (and To review, Create certification); the inspector opens **My jobs** (and Create certification); the coordinator opens **Requests** and also has **Quotations**, **Jobs** (the queue: assign inspectors, deliver certificates) and **Clients** (details, contacts, items, senders, history and the certificate archive). Create certification is not offered to coordinators (the API answers 403).

**Man day, machines added on site, mailbox date.** Coordinators, technical managers and admin create a *Man day* from **Requests** or **Jobs** (`/requests/man-day`, `POST /requests/man-day`): a client, an inspector, a date and a location, no quotation; the job opens at once. On any job that is under way (assigned to returned) the inspector, coordinator, technical manager and admin can **Add a machine** (a registered one of the client, or a new one that is registered for the client immediately; gears are not), **Edit** it (until it is submitted) and **Remove** it (until its certificate was started). Requests and queue rows carry a *Man day* / *Direct certificate* tag, there is a **Man days** tab on Requests, and these jobs never show a price. Technical managers and admin have **Mailbox** (`/mailbox`): per coordinator, the date mail is imported from, with a confirmation before it is changed. The full IMAP form (which needs the password) is not built.

Branding: the app starts with the built-in Integrity identity. After sign-in it asks the public `GET /branding/{managerId}/logo.png`
for the team's own logo and falls back to the built-in one on 404. Colours are not exposed by a public endpoint yet, so they stay on the tokens.

## Known gaps

- Inspectors cannot list clients or items (`/clients` and `/equipment` need a coordinator, manager or admin), so the Create certification client
  picker shows a message for them until the backend offers a lookup for inspectors.
- The coordinator screens are for the coordinator role; admin and technical manager can open the same URLs but have no menu entries for them yet.
- Not built for coordinators: editing the quotation scope text templates, the mailbox (IMAP) connection form (only the import date is built) and the maybe-email sender rules list under `/client-mail-rules` (senders are managed per client).
- Admin creating a certification needs `managerId` in the request; the tab does not send it yet.
- Admin does not pick a team from a list (the API has no list of managers for it): the Man day form and Mailbox ask for the technical manager id.
- The machine list (`MachineSummary`) does not return a machine's location or problem description, so editing a machine shows those two fields empty and clears what is left empty (the PUT replaces all details).
- The machine form response has no `isGear`; the form decides a gear by matching the item name in the cached catalogue.
