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
- The job machines list (`MachineList`) does not carry the job's `kind`, so the job screen itself has no Man day tag; the queue, requests, quotations and review lists do.
