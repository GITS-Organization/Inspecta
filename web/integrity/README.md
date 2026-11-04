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

Roles and start pages: admin and technical manager open **Overview** (and To review, Create certification); the inspector opens **My jobs**
(and Create certification); the coordinator opens **Jobs** (approved and delivered lists).

Branding: the app starts with the built-in Integrity identity. After sign-in it asks the public `GET /branding/{managerId}/logo.png`
for the team's own logo and falls back to the built-in one on 404. Colours are not exposed by a public endpoint yet, so they stay on the tokens.

## Known gaps

- Inspectors cannot list clients or items (`/clients` and `/equipment` need a coordinator, manager or admin), so the Create certification client
  picker shows a message for them until the backend offers a lookup for inspectors.
- The coordinator hand-over (deliver to client) screens are not built; coordinators see the lists and the certificate PDFs.
- Admin creating a certification needs `managerId` in the request; the tab does not send it yet.
