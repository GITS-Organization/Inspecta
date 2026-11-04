# gitsplatform.com

Angular site for GITS: the public pages and the admin area where GITS staff create inspection companies (technical managers). Standalone components, signals, strict TypeScript, SCSS. The plan is in [PLAN.md](PLAN.md).

## Run

```bash
npm install
npm start          # http://localhost:4200, /api is proxied to https://api.gitsplatform.com (proxy.conf.json)
```

Signing in needs a real admin account on that API. Never commit credentials; none are in this repo.

## Test and build

```bash
npm test -- --watch=false   # Vitest through the Angular test runner, HttpClient is mocked
npm run build               # production build into dist/gitsplatform/browser
```

## Configuration

- `src/environments/environment.ts` (development): `apiUrl: '/api/v1'` goes through the proxy.
- `src/environments/environment.production.ts` (replaces it in production builds): `apiUrl: 'https://api.gitsplatform.com/api/v1'`. The API must list `https://gitsplatform.com` in its allowed CORS origins.
- `src/app/site/site-content.ts`: all placeholder copy, contact details and the placeholder mark. GITS has no real logo or brand text yet, so edit only this file (the mark itself is drawn in `styles.scss`, `.mark`).

## Deploy (Firebase Hosting for gitsplatform.com)

`firebase.json` serves `dist/gitsplatform/browser` and rewrites every path to `index.html` (the router handles `/login` and `/admin`). Not deployed from here. When ready:

```bash
npm run build
npx firebase-tools deploy --only hosting
```

Then connect the `gitsplatform.com` domain in the Firebase console.

## What talks to the real API

| Screen | Calls |
| --- | --- |
| Hero verification box (typed code) | `GET /api/v1/verify/{code}` (public; a 404 body is shown as "not found") |
| Admin login | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`. Only role `admin` is kept; others are refused and their tokens revoked. Tokens live in `localStorage`; the interceptor refreshes once on a 401 and signs out if that fails |
| Companies | `GET /users`, `GET /admin/managers/{id}/company-profile` (404 = no profile yet) |
| Add a company | `POST /technical-managers` with `companyProfile` (name+address together or neither, raw base64 PNG up to 1 MB, `#RRGGBB`) |
| Users | `GET /users`, `PUT /users/{id}/status` |
| Email backlog | `POST /admin/email-backlog/cleanup` (dry run first, then delete) |

Server problem details (400/409 bodies) are shown as plain sentences, see `core/problem.ts`.

## Mocked, left out, or not in the API

- The hero's "Scan" code button is a static demo (placeholder certificate in `site-content.ts`).
- There is no "list companies" endpoint: the list is composed from users plus one profile request per technical manager.
- The mockup's web address (subdomain) field and "invitation sent by email" do not exist in the API. The form asks for the manager's first password (12+ characters, with a Generate button) and the admin passes it on.
- Editing an existing company's profile (`PUT /admin/managers/{id}/company-profile`) has a service method but no screen yet.
- Not checked against a live admin account: no credentials were available, so the admin screens are verified with mocked HttpClient tests and in-browser rendering only.
