# Integrity web app — plan

**Goal:** the Angular web client of Integrity (integrity.gitsplatform.com), wired to the real `/api/v1` backend.
**Design source:** `docs/frontend-design/integrity-app.html` (tokens, type, certificate "paper" preview). Not committed upstream; we reproduce it exactly.
**Stack:** Angular (latest stable), standalone components, signals, strict TS, SCSS, Vitest via `ng test`. No NgModules.

## Global constraints
- Tokens: ink `#13285D`, green `#3FA14D`, teal `#1B8FA6`, paper `#F5F7F4`; fonts Barlow Condensed (display) + Public Sans (text).
- Responsive to phone width, keyboard accessible (visible focus), `prefers-reduced-motion` respected.
- Never touch backend files. Dev proxy `/api` -> `https://api.gitsplatform.com`. No credentials in code.
- Enums from the API are snake_case strings; JSON keys are camelCase; the role comes from the `role` JWT claim.

## Structure
```
src/app/core      models, environment access, AuthService (tokens, refresh, role), auth interceptor, guards, BrandingService, ToastService
src/app/api       typed endpoint wrappers: dashboard, queue, machines (+ blob downloads), certification (interface + mock)
src/app/shared    CertificatePaper component (the signature element), status chips, format helpers
src/app/shell     rail navigation per role + outlet
src/app/features  login, overview, jobs, job (machines list), review, machine-form, create
```

## Tasks (each ends with `ng build` + `ng test` green, then a commit + push)
1. **Scaffold** — project, environments (dev `/api/v1`, prod `https://api.gitsplatform.com/api/v1`), `proxy.conf.json`, global design tokens/styles, logo asset.
2. **Core** — AuthService (login, refresh rotation, `me`, logout, role from claim), interceptor (bearer, one refresh on 401, queue concurrent calls), guards (`authGuard`, `roleGuard`), routes, shell with per-role navigation. Tests: role parsing, refresh-once behaviour, guard decisions.
3. **Login + Overview** — `POST /auth/login`; `GET /dashboard/overview` (pipeline bar, attention, months chart, inspector workload, items due).
4. **Jobs + Review** — `GET /quotation-queue?status=`, job machines list, TM review (`/to-review`, `/machines`, PDF/ZIP blob download, `approve-all`, `return-to-inspector`) with the live paper preview.
5. **Machine form** — `GET/PUT/submit .../machines/{requestItemId}`: fields, checklist yes/no/na, questionnaire, texts, dates, result; save + submit; blockers (`missing`).
6. **Create certification** — `CertificationApi` interface + in-memory mock; client, machine (existing/new), examination, gears with quantity/serials split over certificates (pure `splitSerials` util, tested), live paper. Wire for real once the backend contract arrives from the parent session.
7. **Docs & polish** — README (run/build/deploy to Firebase Hosting, SPA rewrite), `firebase.json`, screenshot comparison against the mockup, final verification.

## Review focus (failure modes the design implies)
- Refresh token rotation races: two requests 401 at once must trigger a single refresh. (test)
- Role guard: a coordinator opening `/overview` is redirected to their home, not shown a 403 page. (test)
- Serial split: quantity 0, pasted text with blanks/duplicates, more certificates than serials. (test)
- Blob downloads need the bearer token, so PDFs are fetched via HttpClient and opened from an object URL.
- Return-to-inspector requires a note; approve is disabled while `canApprove` is false and shows `reviewBlockers`.
