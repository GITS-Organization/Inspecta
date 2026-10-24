# Login flow: backend user stories (Firebase → .NET migration)

Audience: the .NET backend developer.
Source: the current Flutter app (`lib/features/auth/*`, `lib/app/router/auth_gate.dart`, `firestore.rules`).

## 1. How login works today (what the .NET API must replace)

| Concern | Today (Firebase) | Where |
|---|---|---|
| Credentials | Firebase Auth `signInWithEmailAndPassword` | `auth_remote_datasource.dart` |
| Profile / role | Firestore `users/{uid}`: `name, email, phone, role, qualifications[{name, validUntil}], signatureImageId, active, lastLogin, onLeaveUntil` | `user_model.dart` |
| Session persistence | Firebase SDK keeps the session. A device-local flag `keep_signed_in` (default true) makes the app sign out on next launch when false. | `auth_local_datasource.dart` |
| Session restore | `authStateChanges()` stream, then the app reads the profile | `auth_repository_impl.dart` |
| Live kick-out | A Firestore listener on `users/{uid}`. If the profile is deactivated or loses its role, the app signs out at once and shows the reason on the sign-in screen. | `auth_repository_impl.dart` `watch()` |
| Role routing | The role picks the home screen: coordinator, inspector, technical_manager. `admin` has no real home yet. Legacy `supervisor` is read as `coordinator`. | `auth_gate.dart`, `user_role.dart` |
| Authorization | Firestore security rules (BR-01.3 role checks, BR-01.4 not deactivated) | `firestore.rules` |
| Forgot password | `sendPasswordResetEmail` with the UI language. Unknown emails are silently treated as success. | `auth_repository_impl.dart` |
| Reset password | Firebase's hosted web page. There is no in-app screen. | n/a |
| Sign-up | None. The sign-in screen says "No account? Ask your admin to add you." | `app_en.arb` |
| Other uses of the auth uid | `createdBy` on quotations, `actorId` on request events | `quotation_remote_datasource.dart`, `requests_remote_datasource.dart` |

Error codes the client already understands and localises (EN/AR). The API should return the same stable codes:
`invalid_email`, `invalid_credentials`, `account_disabled`, `no_profile`, `too_many_attempts`, `network` (client-side), `unavailable`, `unknown`.

Roles: `coordinator`, `inspector`, `technical_manager`, `admin`. Each user has exactly one (BR-01.1).

---

## 1b. Roles

Each user has exactly one role (BR-01.1). The `role` value is stored on the user and returned by login and `/auth/me`. The app picks the home screen from it.
Source for the matrix below: `firestore.rules`, which the .NET authorization must reproduce (BE-AUTH-07).

| Role (API value) | Who they are | Mobile home (bottom bar) | What they can do |
|---|---|---|---|
| `coordinator` | Office staff who run the job pipeline. The old `supervisor` role was merged into this one. | Requests, Quotations, Queue, More (Profile, Clients, Inspectors, Timesheets) | Owns a request from intake to assignment. Creates and edits requests, creates and sends quotations and logs the client's response, assigns inspectors, marks an inspector on leave. Maintains clients. Approves or returns inspector timesheets. Sends an approved certificate to the client. |
| `inspector` | Field staff who do the inspection. | Tasks, Certificates, Timesheet, Profile | Sees only jobs assigned to them. Accepts, declines or starts a job, and drafts and submits the certificate (editable while in progress or returned). Logs a timesheet entry (locked once approved) and sets their own hourly rate. |
| `technical_manager` | Reviewer who signs off certificates. | Review, Sent, Profile | Read-only view of jobs, clients and quotations. Reviews submitted certificates and approves or returns them. Approval issues the certificate number and QR/verification code. Keeps one saved signature (own only). Reads inspector profiles. |
| `admin` | Management and system administrator. | No mobile home yet (placeholder screen). Used by the management dashboard, which is being rebuilt. | Creates, edits and deactivates users and assigns roles. Reads everything: users, requests, certificates, clients, quotations, timesheets and timesheet settings, plus website demo requests. Can create and update clients and decide timesheets. |

Rules that apply to every role
- The `role` claim is only trusted for routing. Authorization reads the user's current `role` and `active` from the database (BR-01.3, BR-01.4).
- A user can always read their own profile, even when deactivated. This lets the app tell them why they were signed out.
- A role is never self-assigned. Only an admin can create users or change `role` and `active`.
- A user with no role, or a role not in this table, cannot sign in (`no_profile`).
- Legacy `supervisor` values are mapped to `coordinator` on read and at migration.

Job-related permission matrix (what each role may do to the main records)

| Record | coordinator | inspector | technical_manager | admin |
|---|---|---|---|---|
| Users | read inspectors; set an inspector's `onLeaveUntil` | read own; stamp own `lastLogin` | read inspectors | create, read, update, delete |
| Requests | create, read, update, delete | read assigned only; accept, decline, start, submit | read; approve or return a submitted certificate | read |
| Quotations | create, read, update (frozen once sent, except client response and reminder fields) | none | read | read |
| Clients | read, create, update | none | read | read, create, update |
| Certificates | read; mark as sent (`sentTo`) once approved | read own, create and edit own while drafting or returned | read; write review fields on submitted ones | read |
| Timesheets | read all; approve or return | read own; create and edit own until approved | none | read; may decide too |
| Timesheet settings (hourly rate) | none | read and set own | none | read |
| Signatures | none | none | read and set own | none |
| Request event log | read; append | append | read; append | read |
| Website demo requests | none | none | none | read |
| Public certificate verification (QR) | anyone, no login, for one issued certificate by its id | same | same | same |

---

## 2. Epic: Authentication API

### BE-AUTH-01: Sign in with email and password
**As** a staff member (coordinator, inspector, technical manager or admin),
**I want** to sign in with my work email and password,
**so that** I land on the home screen for my role.

`POST /auth/login` with `{ email, password, keepSignedIn }`

Acceptance criteria
- The email is trimmed and matched case-insensitively. The password is never trimmed.
- Success returns `200` with `{ accessToken, refreshToken, expiresIn, user }`.
  - `user` = `{ id, name, email, phone, role, qualifications[{name, validUntil}], signatureImageId, active, lastLogin, onLeaveUntil }`.
  - `role` is one of `coordinator`, `inspector`, `technical_manager`, `admin` (see section 1b). Legacy `supervisor` is returned as `coordinator`.
- Malformed email returns `400 invalid_email`.
- Wrong password and unknown email both return `401 invalid_credentials` with the same body and similar response time, so accounts can't be enumerated.
- Credentials are checked before account status. `active = false` returns `403 account_disabled` only after a correct password, so unauthenticated callers can't learn which accounts exist.
- A user with a missing or invalid role returns `403 no_profile`, and no tokens are issued. A missing `active` value means not active (the app already treats it that way).
- Failed attempts are rate limited and locked out. The response is `429 too_many_attempts` with a `Retry-After` header. Suggested: 5 failures per account and IP in 15 minutes. Lockout must never permanently lock a real user out.
- On success, `lastLogin` is updated without blocking the response. A failure to update it must not fail the login.
- Error bodies use one envelope: `{ code, message }`. The client maps `code` to localised text and ignores `message`.
- Every attempt (success or fail) is written to the audit log with timestamp, user id if known, IP and device.

### BE-AUTH-02: "Keep me signed in"
**As** a user,
**I want** to choose whether my session survives closing the app,
**so that** I stay logged in on my own phone but not on a shared one.

Acceptance criteria
- `keepSignedIn = true` (the default) issues a long-lived refresh token (suggested 30 days, sliding).
- `keepSignedIn = false` issues a short session. Suggested: it expires after 12 hours, or the client discards it on next launch.
- The server enforces the lifetime. The device-local flag is only a UX hint.
- Refresh tokens are stored hashed and tied to a device or session record.

### BE-AUTH-03: Refresh the session
**As** the mobile app,
**I want** to renew an expired access token silently,
**so that** the user never sees a login screen while their session is still valid.

`POST /auth/refresh` with `{ refreshToken }`

Acceptance criteria
- Access tokens are short-lived (suggested 15 minutes) JWTs with claims `sub` (user id), `role` and `jti`.
- Refresh tokens rotate on every use. The old token is invalidated.
- Reusing an already-rotated token revokes that whole token family and returns `401`.
- Refresh re-checks the user: `active` and a valid role. If the user is deactivated or has no role, it returns `403 account_disabled` or `403 no_profile` and revokes the tokens.
- The role claim is re-read from the database on refresh, so a role change takes effect within one access-token lifetime at most (see BE-AUTH-06 for immediate effect).

### BE-AUTH-04: Restore session / get current user
**As** the app on launch (splash screen),
**I want** to fetch the signed-in user's profile,
**so that** I can route to the correct role home or to sign-in.

`GET /auth/me` (Bearer token)

Acceptance criteria
- Returns the same `user` object as login.
- `401` for a missing or expired token. The client then tries refresh, then falls back to sign-in.
- `403 account_disabled` or `403 no_profile` if the account became invalid. The client shows that reason on the sign-in screen.
- Fast enough for a splash screen. Target p95 under 300 ms.
- The same user fields feed the Profile screen, so the profile fields can be edited later without changing this contract.

### BE-AUTH-05: Sign out
**As** a user,
**I want** to sign out,
**so that** nobody else can use my session on this device.

`POST /auth/logout` with `{ refreshToken }`

Acceptance criteria
- Revokes that refresh token (that device only). Returns `204`.
- Idempotent: an unknown, expired or already-revoked token still returns `204`.
- The client clears its local session even if this call fails (current behaviour), so the endpoint must tolerate retries.
- Optional: `POST /auth/logout-all` revokes every session for the user.

### BE-AUTH-06: End sessions immediately on deactivation or role change
**As** an admin,
**I want** a deactivated user to lose access right away,
**so that** a leaver can't keep working until their token expires (BR-01.4).

Today the app gets this for free from the live Firestore listener. The .NET API has no listener, so this story replaces it.

Acceptance criteria
- Deactivating a user revokes all of their refresh tokens in the same transaction.
- The next API call from that user returns `403 account_disabled`. Either the active check runs on every request, or access tokens are short enough that the gap is acceptable (stakeholder to confirm the max gap).
- Role change: the user's next token refresh carries the new role. Authorization always reads the current role from the database, not only from the token.
- Optional, for parity with today's instant kick-out: push a "session invalidated" event to the app (SignalR or FCM). If skipped, the user is signed out on the next API call (acceptable gap to be confirmed).
- `lastLogin`, `role` and `active` changes are audit-logged with the admin who made them.

### BE-AUTH-07: Role-based authorization on every endpoint
**As** the business,
**I want** the server to enforce who can do what,
**so that** access control does not depend on the app hiding buttons (BR-01.3).

Acceptance criteria
- Every non-auth endpoint requires a valid token and `active = true`.
- Each endpoint declares its allowed roles (`[Authorize(Roles = ...)]` or policies). The role definitions and permission matrix in section 1b are the reference, and `firestore.rules` has the exact conditions: port them one by one and list each rule's .NET equivalent.
- Ownership rules are enforced too, not only the role. For example, an inspector only sees jobs where `inspectorId` is their own id, and a technical manager can only touch a certificate while its job is awaiting review.
- Some rules depend on the record's state (for example an inspector can edit a certificate only while the job is `in_progress` or `certificate_returned`). The API must check state as well as role.
- Unauthorised role returns `403 forbidden`. Unauthenticated returns `401`.
- Tests cover each role against each protected endpoint.

### BE-AUTH-08: Forgot password (request reset link)
**As** a user who forgot their password,
**I want** to request a reset link by email,
**so that** I can get back in without asking an admin.

`POST /auth/forgot-password` with `{ email, language }` where `language` is `en` or `ar`

Acceptance criteria
- Always returns `202`, with the same body and similar timing, whether or not the email exists (no account enumeration). This matches today's behaviour.
- If the account exists and is active, an email is sent in the requested language (EN or AR; RTL layout for AR).
- The link carries a single-use token that expires (suggested 60 minutes), stored hashed.
- Requesting a new link invalidates the previous one.
- Rate limited per email and per IP (for example 3 per hour). Excess requests still return `202` but send nothing.
- Deactivated accounts get no email, and the response is still `202`.
- Invalid email shape returns `400 invalid_email` (the client validates first, so this is rare).

### BE-AUTH-09: Reset password (complete the reset)
**As** a user with a reset link,
**I want** to set a new password,
**so that** I can sign in again.

Today Firebase hosts this page. After migration the backend owns it, so a decision is needed (see Open questions): a web page served by the API, or an in-app screen opened by a deep link.

`POST /auth/reset-password` with `{ token, newPassword }`

Acceptance criteria
- A valid, unexpired, unused token sets the password and marks the token used. Returns `204`.
- An invalid, expired or used token returns `400 invalid_token`.
- Password policy is enforced server-side and returned as a clear error code (policy to be agreed: minimum length, etc.). The current app has no password rules, only "required".
- On success all of the user's refresh tokens are revoked, and a "your password was changed" email is sent.
- Works for EN and AR users; the hosted page (if any) is localised and RTL-aware.

### BE-AUTH-10: Move existing users from Firebase without breaking anything
**As** a current user,
**I want** my account to keep working after the migration,
**so that** I don't lose access or my history.

Acceptance criteria
- Firebase `uid` values are preserved as the user id (or a mapping table is kept). They are already stored as `createdBy` on quotations and `actorId` on request events, so they must not change.
- All `users/{uid}` fields migrate: `name, email, phone, role, qualifications (including legacy bare-string entries), signatureImageId, active, lastLogin, onLeaveUntil`.
- Legacy `role = supervisor` becomes `coordinator`.
- Passwords: export Firebase password hashes (scrypt) and verify them with a compatible hasher, re-hashing to the new algorithm on first successful login. If that isn't feasible, fall back to a forced reset (BE-AUTH-08) for every user, with a communication plan.
- Disabled Firebase accounts stay disabled.
- Cutover plan covers: freeze window, delta migration, how app versions behave during the switch (old builds still talk to Firebase), and rollback.
- Seeded test accounts used by QA (such as `inspector@inspecta.test`) exist in dev and staging.

### BE-AUTH-11: Contract, security and operations
**As** the mobile team,
**I want** a stable, documented auth contract,
**so that** we can switch the app over with a small, predictable change.

Acceptance criteria
- OpenAPI spec published for all `/auth/*` endpoints, with example responses for each error code.
- HTTPS only; HSTS enabled.
- The app has no certificate pinning today. If it is added, the API's CA must be in the pinned set before release.
- Token and password values never appear in logs.
- Passwords hashed with a modern algorithm (Argon2id, or PBKDF2 or bcrypt with sane cost).
- CORS only if a web client exists.
- Health endpoint and structured logging for auth failures. Alerts on login-failure spikes.
- Error responses are localised by the client from `code`. The server does not need to translate `message`.

---

## 3. Out of scope for this epic
- User creation, editing, deactivation and role assignment screens: admin-provisioned today (the sign-in screen says "ask your admin to add you"). Needs its own epic, but BE-AUTH-06 and BE-AUTH-10 depend on it.
- Profile edit (phone, signature upload) and profile stats.
- MFA / SSO.
- Self-service sign-up.

## 4. Open questions for the product owner and BE lead
1. **Reset-password page**: hosted by the API, or in-app via deep link?
2. **Password policy**: what minimum rules?
3. **Acceptable gap** between deactivation and the user being kicked out: instant (push event or check on every request) or "on next API call / within 15 minutes"?
4. **Session lengths** for keep-signed-in on or off.
5. **Password migration**: hash import with re-hash on login, or forced reset for everyone?
6. **Admin role**: it exists in data but has no real home screen in the app, and the management dashboard that used it is being rebuilt. Should admin be able to sign in on mobile at all, or only on the new dashboard?
7. **Offline**: should a cached session be usable offline? Today Firebase tolerates short offline periods, since the token is cached locally.
