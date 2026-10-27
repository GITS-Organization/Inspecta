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
dotnet user-secrets set "ConnectionStrings:Inspecta" "Host=localhost;Database=inspecta;Username=postgres;Password=<local-password>" --project .\Inspecta.Api\Inspecta.Api.csproj
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

## Deploying a test instance to Render

The repository includes a [`render.yaml`](./render.yaml) Blueprint for the `backend` branch. In Render, choose **New > Blueprint**, connect this GitHub repository, select the `backend` branch, and review the Blueprint before deploying. It creates a Docker web service, configures `/health` as its health check, generates a JWT signing key, and asks for the PostgreSQL connection string and initial Admin email, full name, and password.

Create a PostgreSQL database in Render in the same region as the web service. Copy its internal connection details into `ConnectionStrings__Inspecta` using the ADO.NET format `Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require`. Review Render's current database pricing and free-tier limits before creating it. The API creates/updates schema from its PostgreSQL migrations on startup; this starts with an empty database and does not copy data from the old SQLite file.

If creating the service manually instead of using the Blueprint, select the `backend` branch and Docker runtime with `Dockerfile` at the repository root. Set `ASPNETCORE_HTTP_PORTS=8080`, `PORT=8080`, `ConnectionStrings__Inspecta` to the PostgreSQL connection string, `Jwt__Issuer=Inspecta.Api`, `Jwt__Audience=Inspecta.Client`, and `Swagger__Enabled=true`. Set `Jwt__SigningKey` to a unique secret with at least 32 UTF-8 bytes and set all three `BootstrapAdmin__Email`, `BootstrapAdmin__FullName`, and `BootstrapAdmin__Password` values for the first deployment. Never commit these secrets. For Flutter Web, add `Cors__AllowedOriginsCsv` with the exact HTTPS origin of the deployed frontend; native Flutter apps do not need CORS.

## API request examples

See [Inspecta.Api.http](./Inspecta.Api/Inspecta.Api.http) for example requests for every API endpoint. Replace the sample IDs and credentials with values from your environment. Enum values are JSON strings (for example `technical_manager`, `inspector`).

## Data and account notes

- Bootstrap admin creation is optional and only creates an account when the configured email is not already present. The account can sign in through `POST /api/v1/auth/login`; keep its credentials private and replace the password after testing.
