# gitsplatform.com site and admin: plan

**Goal:** public marketing site plus an admin area for GITS staff, wired to the Inspecta API (`/api/v1`).
**Design source:** `docs/frontend-design/gitsplatform-site.html` (kept outside the repo). Tokens: paper `#F3F2EC`, ink `#13171F`, blue `#2B47F0`, tape `#FFC400`; Bricolage Grotesque + Hanken Grotesk. The "scan to verify" card is the one signature element; everything else stays quiet.
**Stack:** Angular (standalone, signals, strict TS, SCSS), Vitest through `ng test`, Firebase Hosting (SPA rewrite).

## Layout
- `core/` api models, `problem.ts` (turn a server problem+json into one plain message), `auth.service` (JWT in localStorage, refresh), `auth.interceptor` (bearer + single shared refresh on 401), `guards` (`adminGuard`, `guestGuard`), `admin-api.service`, `verify.service`.
- `site/` `site-content.ts` (ALL placeholder copy, mark, contacts: the one file to edit), public shell, home sections, verification demo.
- `admin/` login, shell (sidebar), companies list, add-a-company form (live preview), users, email backlog.

## Tasks (each ends with tests green + commit + push)
1. Scaffold, tokens, env files, proxy, firebase.json. `ng build` passes.
2. Core: problem messages, auth service, interceptor, guards, with mocked-HttpClient specs (login role check, refresh once, 401 -> sign out, 409/400 message).
3. Public site: shell, hero + verification demo (real `GET /verify/{code}`, 404 body is still a result), what we do, clients, about, contact. Spec for verify service.
4. Admin login + shell + guards on routes.
5. Companies list: `GET /users?role=technical_manager`, `GET /users` (team counts), `GET /admin/managers/{id}/company-profile` (404 = built-in).
6. Add a company: `POST /technical-managers` with `companyProfile` (name+address together or neither = built-in details; raw base64 PNG <= 1 MB; `#RRGGBB`). Spec for the request builder.
7. Users (list/filter, enable/disable) and Email backlog (dry run first, then delete).
8. Screenshots against the mockup, README, final verification, push.

## Not available in the API (left out or mocked, listed in README)
Web address / subdomain per company, invitation emails (the admin sets the first password), a dedicated list-companies endpoint (composed from users + profiles).
