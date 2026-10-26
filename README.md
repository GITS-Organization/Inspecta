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
| Companies | `GET /companies`, `POST /companies` | Admin |
| Users | `GET /users`, `POST /users`, `GET /users/{userId}`, `PUT /users/{userId}/profile`, `DELETE /users/{userId}` | Admin |
| Manager team | `GET /manager/team`, `POST /manager/team`, `GET /manager/team/{userId}`, `PUT /manager/team/{userId}`, `DELETE /manager/team/{userId}` | Technical Manager; scoped to their company |
| Clients | `GET /clients`, `POST /clients`, `GET /clients/{clientId}`, `DELETE /clients/{clientId}` | Admin, Coordinator |
| Equipment/items | `GET /equipment`, `POST /equipment`, `GET /equipment/{equipmentId}`, `PUT /equipment/{equipmentId}`, `POST /equipment/{equipmentId}/inspections`, `DELETE /equipment/{equipmentId}` | Admin, Coordinator |
| Inspection requests | `GET /requests`, `POST /requests`, `GET /requests/{requestId}`, `POST /requests/{requestId}/transitions`, `POST /requests/{requestId}/assignment` | Authenticated; create/assign requires Admin or Coordinator |
| Quotes | `GET /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes`, `POST /requests/{requestId}/quotes/{quoteId}/send`, `POST /requests/{requestId}/quotes/{quoteId}/decision` | Admin, Coordinator |
| Certificates | `GET /requests/{requestId}/certificate`, `PUT /requests/{requestId}/certificate/draft`, `POST /requests/{requestId}/certificate/submit`, `POST /requests/{requestId}/certificate/review` | Authenticated; action-specific roles enforced |
| Timesheets | `GET /requests/{requestId}/timesheet`, `PUT /requests/{requestId}/timesheet`, `POST /requests/{requestId}/timesheet/review` | Authenticated; action-specific roles enforced |

Successful create operations return `201 Created`; archive/deactivate actions return `204 No Content`. API errors use Problem Details JSON.

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

## API request examples

See [Inspecta.Api.http](./Inspecta.Api/Inspecta.Api.http) for example requests for every API endpoint. Replace the sample IDs and credentials with values from your environment. Enum values are JSON strings (for example `technical_manager`, `inspector`).

## Data and account notes

- The runtime has no Firebase or Flutter dependency. The `RemoveFirebaseIdentity` migration drops the legacy Firebase UID column from `Users`; back up existing databases before applying it if those IDs are still needed for external reconciliation.
- Existing users have no company assigned by default. An Admin must provision companies and assign existing users explicitly.
- Bootstrap admin creation is optional and only creates an account when the configured email is not already present.
