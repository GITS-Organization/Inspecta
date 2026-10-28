# Inspecta .NET API

ASP.NET Core 8 REST API for Inspecta. This repository contains the backend only; web or mobile clients consume the API over HTTP.

## API documentation

- Swagger UI: `/swagger`
- OpenAPI JSON: `/swagger/v1/swagger.json`
- Health check: `/health`

Swagger is enabled by default in every environment so a separately hosted frontend and API consumers can discover the contract. Set `Swagger__Enabled=false` to disable it. Authenticated endpoints use JWT bearer tokens; use **Authorize** in Swagger UI and paste an access token.

## API surface

All business routes use the `/api/v1` prefix. Roles in the table are required JWT roles.

| Area | Routes | Access |
|---|---|---|
| Authentication | `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me`, `POST /auth/logout` | Public login/refresh; authenticated profile/logout |
| Technical managers | `POST /technical-managers`, `PUT /technical-managers/{managerId}/subscription` | Admin |
| Users | `GET /users`, `POST /users`, `GET /users/{userId}`, `PUT /users/{userId}/profile`, `PUT /users/{userId}/status`, `DELETE /users/{userId}` | Admin |
| Manager team | `GET /manager/team`, `POST /manager/team`, `GET /manager/team/{userId}`, `PUT /manager/team/{userId}`, `PUT /manager/team/{userId}/status`, `DELETE /manager/team/{userId}` | Technical Manager (own team); Admin (include `?managerId={managerId}`) |
| Coordinator mailbox | `PUT /manager/team/{coordinatorId}/email-config` | Technical Manager (own team); Admin (include `?managerId={managerId}`) |
| Clients | `GET /clients`, `POST /clients`, `GET /clients/{clientId}`, `GET /clients/{clientId}/history`, `PUT /clients/{clientId}`, `DELETE /clients/{clientId}` | Admin, Technical Manager, Coordinator; team scoped |
| Equipment/items | `GET /equipment`, `POST /equipment`, `GET /equipment/{equipmentId}`, `PUT /equipment/{equipmentId}`, `POST /equipment/{equipmentId}/inspections`, `DELETE /equipment/{equipmentId}` | Admin, Technical Manager, Coordinator; team scoped |
| Inspection requests | `GET /requests`, `POST /requests`, `GET /requests/{requestId}`, `GET /requests/{requestId}/email`, `PUT /requests/{requestId}`, `POST /requests/{requestId}/items`, `PUT /requests/{requestId}/items/{itemId}`, `DELETE /requests/{requestId}/items/{itemId}`, `POST /requests/{requestId}/transitions`, `POST /requests/{requestId}/assignment` | Admin, Technical Manager, Coordinator; assigned Inspectors can view and update their own tasks |
| Test incoming email | `POST /emails/send-test` | Admin, Technical Manager, Coordinator |
| Quotes | `GET /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes/{quoteId}/send`, `POST /requests/{requestId}/quotes/{quoteId}/decision` | Admin, Technical Manager, Coordinator; team scoped |
| Quotation drafts (legacy) | `GET /quotation-drafts`, `PUT /quotation-drafts/{draftId}`, `POST /quotation-drafts/{draftId}/decision` | GET: Coordinator (own drafts), Technical Manager (own team), Admin (all tenants or selected `?managerId={managerId}`). PUT/decision: owning Coordinator only |
| Certificates | `GET /requests/{requestId}/certificates`, `GET /requests/{requestId}/certificates/{requestItemId}`, `PUT /requests/{requestId}/certificates/{requestItemId}/draft`, `POST /requests/{requestId}/certificates/{requestItemId}/submit`, `POST /requests/{requestId}/certificates/{requestItemId}/review` | Team scoped; Inspector edits assigned jobs, Admin/Technical Manager review |

Successful create operations return `201 Created`; delete actions return `204 No Content`. API errors use Problem Details JSON.

Each client retains its inspection request history. A request can have a certificate for each request item; submit all item certificates before the request enters technical review. Existing certificates are retained by the migration and linked to a request item when the match is unambiguous.

`POST /api/v1/emails/send-test` is an authenticated test helper that generates a realistic email body and simulates the incoming-email webhook by creating an email-sourced inspection request. It saves the generated body as the email preview and stores the reported machine issue on the request item. Coordinator IMAP mailboxes use a separate parser and create quotation drafts through the background sync workflow below.

### Coordinator email-to-request workflow

- When a Technical Manager creates a Coordinator using `POST /api/v1/manager/team`, include `emailConfig` with `imapServer`, `imapPort`, `emailUsername`, `password`, and `useSsl`. IMAP configuration is required for Coordinators and must not be sent for Inspectors. Passwords are encrypted with AES-GCM before being saved and are never returned by the API.
- Set `IMAP_ENCRYPTION_KEY` in Render as a stable Base64-encoded key containing exactly 32 random bytes. Configure the same value in `.env` for Docker deployments, or in user secrets for local development. Generate one with PowerShell:

  ```powershell
  $key = New-Object byte[] 32
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)
  [Convert]::ToBase64String($key)
  ```

- Keep the same key across deployments and backups. If it is lost or changed, existing IMAP passwords cannot be decrypted; rotate mail passwords and reconfigure each Coordinator before replacing the key.
- Update mailbox settings through `PUT /api/v1/manager/team/{coordinatorId}/email-config`. Admins must add `?managerId={managerId}`. The API requires TLS: `useSsl=true` uses implicit TLS and `useSsl=false` requires STARTTLS.
- `EmailSyncBackgroundService` polls unread messages every 120 seconds by default (`EmailSync__IntervalSeconds` may be configured from 30 to 3600). It reads the INBOX and creates one `InspectionRequest` per IMAP UID (`source: email`, `status: request_received`), then marks the message read. A unique coordinator/UIDVALIDITY/UID constraint on `RequestEmails` prevents duplicates after a retry, and a message that fails to import is logged and skipped so it never blocks the messages after it or other mailboxes.
- The request is owned by the Coordinator whose mailbox received it (`coordinatorId`) and belongs to that Coordinator's Technical Manager team (`managerId`); both come from the mailbox configuration, never from the email. No quote or quotation draft is created.
- `clientName`, `location` and `items` start empty. `missingFields` on the request lists what is still needed (`clientName`, `location`, `items`); a request cannot move to `quote_draft` until it is empty. Manual requests keep their existing strict creation rules.
- The original message is stored in `RequestEmails` and returned by `GET /api/v1/requests/{requestId}/email`: sender and display name, To/CC/BCC/Reply-To, subject, Message-ID/In-Reply-To/References, sent date, plain-text and HTML bodies, and attachment names/types/sizes. Attachment content is not stored. `htmlBody` is untrusted sender content: sanitize it or render `textBody`. The request itself carries `emailFrom`, `emailFromName`, `emailPreview` (first 300 characters, single line) and `receivedAt` for list and detail screens.
- The owning Coordinator completes the request with `PUT /api/v1/requests/{requestId}` (`clientId`, `clientName`, `location`, `preferredDate`, `accessNotes`) and `POST|PUT|DELETE /api/v1/requests/{requestId}/items[/{itemId}]` (`name`, `quantity`, `type`, `capacity`, `serialNumber`, `location` for the equipment location, `issueDescription`). These return the updated request. Editing is allowed only while the request is `request_received` or `quote_draft`; afterwards the API answers `409`. Linking a `clientId` requires a client of the same team.
- Visibility: a Coordinator sees the manual requests of their team and only the email requests that arrived in their own mailbox; other Coordinators' email requests answer `404`, including on transitions. A Technical Manager sees their whole team and an Admin sees every team (`GET /api/v1/requests?managerId=` narrows it). Only the owning Coordinator can edit an email request; managers and admins have read access. Team scope comes from the JWT tenant claim and the data-layer tenant filter, not from request bodies.
- Quotation drafts from the earlier workflow stay in the database and the `/quotation-drafts` endpoints still read and update them, but no new drafts are created. A message that was already imported as a draft is not imported again as a request.
- Not part of this stage: quote revisions, SMTP sending, PDF generation, and matching client replies to sent quotes. A reply that arrives now is imported as a new request.

### Technical managers, subscriptions, and user lifecycle

- A Technical Manager account represents the company using Inspecta and owns its subscription. The subscription has an explicit `Active`, `Suspended`, or `Expired` status and an optional end date. An active subscription becomes effectively expired when its end date is reached.
- Admin provisions a Technical Manager account through `POST /api/v1/technical-managers`; the subscription defaults to one month and can be set to 1–120 months. Admin can later update its status or end date through `PUT /api/v1/technical-managers/{managerId}/subscription`.
- A suspended or expired manager subscription blocks the manager and all assigned team members from using the API. Team accounts are linked directly to their manager through `ManagerId`; `CreatedById` remains audit information.
- Access tokens contain the user ID and role; team-member tokens also contain manager and tenant IDs, while a Technical Manager token uses that manager's own ID as its tenant. The API checks these claims against the current account and assignment on every authenticated request.
- Client, request, equipment, quote, certificate, contact, request-item, and history queries are scoped to the current tenant. Admin has global access. Existing client/request records without an owner remain visible to Admin only and are intentionally not auto-assigned during migration. There is no API for assigning historical ownership; use an explicitly reviewed data migration or administrative database operation before granting team access to those records.
- When publishing this authorization change, apply the included migrations before or as part of the rollout. Existing access tokens without tenant claims will be rejected; users must refresh their token or sign in again.
- User delete routes perform soft deletes, recording `IsDeleted` and `DeletedAt` instead of removing records. Deleted users are excluded from normal lists; Admin can pass `includeDeleted=true` to the user list route to include them.
- Disabling a user's `IsActive` account is separate from deleting it. Admin can change user status with `PUT /api/v1/users/{userId}/status`; a Technical Manager can change the status of a member in their own team with `PUT /api/v1/manager/team/{userId}/status`.
- Clients expose `isCompany` to identify company clients and `hasContract` to indicate whether a contract is in place; both default to `false`.

## Local development

Requires the .NET 8 SDK. From the repository root, configure a signing key and first admin with user secrets:

```powershell
dotnet user-secrets init --project .\Inspecta.Api\Inspecta.Api.csproj
$randomBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($randomBytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($randomBytes)) --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "ConnectionStrings:Inspecta" "Host=localhost;Database=inspecta;Username=postgres;Password=<local-password>" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:FullName" "Inspecta Admin" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-password>" --project .\Inspecta.Api\Inspecta.Api.csproj
$mailKeyBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($mailKeyBytes)
dotnet user-secrets set "IMAP_ENCRYPTION_KEY" ([Convert]::ToBase64String($mailKeyBytes)) --project .\Inspecta.Api\Inspecta.Api.csproj
```

Passwords must be at least 12 characters and include uppercase, lowercase, numeric, and symbol characters. Never put credentials or signing keys in `appsettings.json`.

```powershell
dotnet build .\InspectaBackend.slnx
dotnet test .\InspectaBackend.slnx
$env:Jwt__SigningKey = "<at-least-32-random-bytes>"
dotnet run --project .\Inspecta.Api\Inspecta.Api.csproj
```

Run a local PostgreSQL instance before starting the API. Local connection strings belong in user secrets, not `appsettings.json`. Local Swagger is available at `http://localhost:5238/swagger`; EF Core applies PostgreSQL migrations at startup.

## Linux deployment with Docker

Docker and Docker Compose are provided as a portable Linux deployment path; no hosting provider is assumed.

1. Copy `.env.example` to `.env`.
2. Set a unique `JWT_SIGNING_KEY` with at least 32 random UTF-8 bytes. Set the bootstrap admin values before the first start.
3. Start the API:

```bash
docker compose up --build -d
```

4. Verify `http://localhost:8080/health`, then open `http://localhost:8080/swagger`.
5. Put the API behind a TLS-enabled reverse proxy or the hosting platform's HTTPS ingress before exposing it publicly.

Set `API_PORT` to change the host port. Production secrets must be supplied by the server's secret manager or environment rather than committed `.env` files. Configure `AllowedHosts`, `Jwt__Issuer`, and `Jwt__Audience` for the deployed domain/client.
For a browser-based frontend on a different origin, set `CORS_ALLOWED_ORIGINS` to a comma-separated list of exact origins (scheme and host, such as `https://app.example.com`). Native clients and server-to-server consumers do not require CORS.

The API uses PostgreSQL. Set `INSPECTA_CONNECTION_STRING` to a PostgreSQL ADO.NET connection string before starting Docker Compose. Keep production credentials in the hosting provider's secret/environment settings, and use managed backups for important data.

## GitHub and Render deployment readiness

The deployment files target the `backend` branch and repository root. Before pushing:

1. Review `git status` and make sure all intended source files and migrations are included in the commit. This workspace may contain uncommitted changes from ongoing development; do not assume a push or deploy includes them until they are committed.
2. Never commit `.env`, database credentials, JWT signing keys, or the bootstrap Admin password. `.gitignore` excludes `.env` files; `.env.example` contains placeholders only. If a real secret was ever committed, rotate it even after removing it from the working tree.
3. Run the Release build and test commands below. Validate the Docker image locally when a Docker daemon is available.
4. Confirm the GitHub repository has the `backend` branch and that the Render Blueprint is configured for that branch.

```powershell
dotnet build .\InspectaBackend.slnx --configuration Release
dotnet test .\InspectaBackend.slnx --configuration Release
git diff --check
```

The Blueprint uses the Dockerfile at the repository root, exposes port `8080`, and probes `/health`. Render must be able to build the full repository context (not only the API subfolder).

## Deploying to Render

The repository includes a [`render.yaml`](./render.yaml) Blueprint for the `backend` branch. Push the committed changes to GitHub first. In Render, choose **New > Blueprint**, connect this GitHub repository, select the `backend` branch, and review the Blueprint before deploying. It creates a Docker web service, configures `/health` as its health check, generates a JWT signing key, and asks for the PostgreSQL connection string and optional initial Admin email, full name, and password. If setting the bootstrap Admin, provide all three values together; the password must satisfy the API password policy.

Create a PostgreSQL database in Render in the same region as the web service. Copy its internal connection details into `ConnectionStrings__Inspecta` using the ADO.NET format `Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require`. Review Render's current database pricing and free-tier limits before creating it. The API creates/updates schema from its PostgreSQL migrations on startup; this starts with an empty database and does not copy data from the old SQLite file. The Render Blueprint creates the web service, not the database: create PostgreSQL in the same region first and then provide its internal connection string in the Blueprint settings. Review Render's current database pricing, free-tier limits, and database retention before creating it.

If creating the service manually instead of using the Blueprint, select the `backend` branch and Docker runtime with `Dockerfile` at the repository root. Set `ASPNETCORE_HTTP_PORTS=8080`, `PORT=8080`, `ConnectionStrings__Inspecta` to the PostgreSQL connection string, `Jwt__Issuer=Inspecta.Api`, `Jwt__Audience=Inspecta.Client`, and `Swagger__Enabled=true` if you want Swagger exposed. Set `Jwt__SigningKey` to a unique secret with at least 32 UTF-8 bytes. Bootstrap Admin settings are optional, but set `BootstrapAdmin__Email`, `BootstrapAdmin__FullName`, and `BootstrapAdmin__Password` together for the first deployment. Never commit these secrets. For Flutter Web, add `Cors__AllowedOriginsCsv` with the exact HTTPS origin of the deployed frontend; native Flutter apps do not need CORS.

After deployment, verify the Render service is healthy, check its logs for successful database migrations, call `/health`, and test login plus one protected endpoint. Confirm that tenant users see only their own assigned data and that Admin can see legacy unassigned records. Rotate or remove the bootstrap Admin password setting after the initial account is created, and keep a separate database backup/restore plan.

## API request examples

See [Inspecta.Api.http](./Inspecta.Api/Inspecta.Api.http) for example requests for every API endpoint. Replace the sample IDs and credentials with values from your environment. Enum values are JSON strings (for example `technical_manager`, `inspector`).

## Data and account notes

- Bootstrap admin creation is optional and only creates an account when the configured email is not already present. The account can sign in through `POST /api/v1/auth/login`; keep its credentials private and replace the password after testing.
