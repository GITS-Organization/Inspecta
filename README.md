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
| Manager team | `GET /manager/team`, `POST /manager/team`, `GET /manager/team/{userId}`, `PUT /manager/team/{userId}`, `PUT /manager/team/{userId}/status`, `DELETE /manager/team/{userId}` | Technical Manager; scoped to their account |
| Clients | `GET /clients`, `POST /clients`, `GET /clients/{clientId}`, `DELETE /clients/{clientId}` | Admin, Coordinator |
| Equipment/items | `GET /equipment`, `POST /equipment`, `GET /equipment/{equipmentId}`, `PUT /equipment/{equipmentId}`, `POST /equipment/{equipmentId}/inspections`, `DELETE /equipment/{equipmentId}` | Admin, Coordinator |
| Inspection requests | `GET /requests`, `POST /requests`, `GET /requests/{requestId}`, `POST /requests/{requestId}/transitions`, `POST /requests/{requestId}/assignment` | Authenticated; create/assign requires Admin or Coordinator |
| Quotes | `GET /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes/{quoteId}/send`, `POST /requests/{requestId}/quotes/{quoteId}/decision` | Admin, Coordinator |
| Certificates | `GET /requests/{requestId}/certificate`, `PUT /requests/{requestId}/certificate/draft`, `POST /requests/{requestId}/certificate/submit`, `POST /requests/{requestId}/certificate/review` | Authenticated; action-specific roles enforced |

Successful create operations return `201 Created`; delete actions return `204 No Content`. API errors use Problem Details JSON.

### Technical managers, subscriptions, and user lifecycle

- A Technical Manager account represents the company using Inspecta and owns its subscription. The subscription has an explicit `Active`, `Suspended`, or `Expired` status and an optional end date. An active subscription becomes effectively expired when its end date is reached.
- Admin provisions a Technical Manager account through `POST /api/v1/technical-managers`; the subscription defaults to one month and can be set to 1–120 months. Admin can later update its status or end date through `PUT /api/v1/technical-managers/{managerId}/subscription`.
- A suspended or expired manager subscription blocks the manager and all assigned team members from using the API. Team accounts are linked directly to their manager through `ManagerId`; `CreatedById` remains audit information.
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
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:FullName" "Inspecta Admin" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-password>" --project .\Inspecta.Api\Inspecta.Api.csproj
```

Passwords must be at least 12 characters and include uppercase, lowercase, numeric, and symbol characters. Never put credentials or signing keys in `appsettings.json`.

```powershell
dotnet build .\InspectaBackend.slnx
dotnet test .\InspectaBackend.slnx
$env:Jwt__SigningKey = "<at-least-32-random-bytes>"
dotnet run --project .\Inspecta.Api\Inspecta.Api.csproj
```

Local Swagger is available at `http://localhost:5238/swagger`. EF Core applies migrations at startup. The default SQLite database is `Inspecta.Api/inspecta.db`.

## Linux deployment with Docker

Docker and Docker Compose are provided as a portable Linux deployment path; no hosting provider is assumed.

1. Copy `.env.example` to `.env`.
2. Set a unique `JWT_SIGNING_KEY` with at least 32 random UTF-8 bytes. Set the bootstrap admin values before the first start.
3. Start the API:

```bash
docker compose up --build -d
```

4. Verify `http://localhost:8080/health`, then open `http://localhost:8080/swagger`.
5. Put the API behind a TLS-enabled reverse proxy or the hosting platform's HTTPS ingress before exposing it publicly. Persist the `/data` volume and back it up.

Set `API_PORT` to change the host port. Production secrets must be supplied by the server's secret manager or environment rather than committed `.env` files. Configure `AllowedHosts`, `Jwt__Issuer`, and `Jwt__Audience` for the deployed domain/client.
For a browser-based frontend on a different origin, set `CORS_ALLOWED_ORIGINS` to a comma-separated list of exact origins (scheme and host, such as `https://app.example.com`). Native clients and server-to-server consumers do not require CORS.

The default provider is SQLite on a persistent Docker volume, suitable for initial low-volume hosting. Assess PostgreSQL or SQL Server and rehearse provider-specific migrations before scaling or running multiple API replicas.

## Deploying a test instance to Render

The repository includes a [`render.yaml`](./render.yaml) Blueprint for the `backend` branch. In Render, choose **New > Blueprint**, connect this GitHub repository, select the `backend` branch, and review the Blueprint before deploying. It creates a Docker web service, configures `/health` as its health check, generates a JWT signing key, and asks for the initial Admin email, full name, and password.

The Blueprint intentionally uses Render's **Free** plan for API testing. Free web services sleep when idle and have an ephemeral filesystem: the SQLite database can be erased when the service sleeps, restarts, or redeploys. Do not store real or important data in this test deployment. Render Free services cannot use persistent disks. To keep SQLite data, upgrade the service to a paid compute plan and attach a persistent disk mounted at `/data`; review Render's current pricing before upgrading. For a production deployment, use a managed database such as PostgreSQL or deploy to a VPS with persistent storage and backups.

If creating the service manually instead of using the Blueprint, select the `backend` branch and Docker runtime with `Dockerfile` at the repository root. Set `ASPNETCORE_HTTP_PORTS=8080`, `PORT=8080`, `ConnectionStrings__Inspecta=Data Source=/data/inspecta.db`, `Jwt__Issuer=Inspecta.Api`, `Jwt__Audience=Inspecta.Client`, and `Swagger__Enabled=true`. Also set `Jwt__SigningKey` to a unique secret with at least 32 UTF-8 bytes and set all three `BootstrapAdmin__Email`, `BootstrapAdmin__FullName`, and `BootstrapAdmin__Password` values for the first deployment. Never commit these secrets. For Flutter Web, add `Cors__AllowedOriginsCsv` with the exact HTTPS origin of the deployed frontend; native Flutter apps do not need CORS.

## API request examples

See [Inspecta.Api.http](./Inspecta.Api/Inspecta.Api.http) for example requests for every API endpoint. Replace the sample IDs and credentials with values from your environment. Enum values are JSON strings (for example `technical_manager`, `inspector`).

## Data and account notes

- The runtime has no Firebase or Flutter dependency. The `RemoveFirebaseIdentity` migration drops the legacy Firebase UID column from `Users`; back up existing databases before applying it if those IDs are still needed for external reconciliation.
- The Company table has been replaced by subscriptions on Technical Manager accounts. The migration transfers each company's subscription to its Technical Manager and moves its team members to a direct manager assignment; deleted companies become expired manager subscriptions.
- Bootstrap admin creation is optional and only creates an account when the configured email is not already present.
