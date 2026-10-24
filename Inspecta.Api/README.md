# Inspecta Backend

ASP.NET Core API for the Flutter application using Clean Architecture and a modular monolith layout:

- `Inspecta.Domain`: workflow entities and invariants.
- `Inspecta.Application`: use cases, contracts, and repository interfaces.
- `Inspecta.Infrastructure`: EF Core SQLite persistence, password hashing, and JWT issuance.
- `Inspecta.Api`: versioned HTTP controllers and authorization.
- `Inspecta.Domain.Tests`: domain behavior tests.

## Local setup

Requires .NET 8 SDK or later. From the repository root, initialize user secrets once and configure a signing key:

```powershell
dotnet user-secrets init --project .\Inspecta.Api\Inspecta.Api.csproj
$randomBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($randomBytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($randomBytes)) --project .\Inspecta.Api\Inspecta.Api.csproj
```

To seed the first admin, set all four values through user secrets or environment variables. Use the actual Firebase UID for the staff member:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:FullName" "Inspecta Admin" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:FirebaseUid" "<existing-firebase-uid>" --project .\Inspecta.Api\Inspecta.Api.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-password>" --project .\Inspecta.Api\Inspecta.Api.csproj
```

Passwords must have at least 12 characters with uppercase, lowercase, numeric, and symbol characters. Bootstrap is optional, has no demo default, and does not overwrite existing accounts. Never put signing keys or passwords in `appsettings.json`.

Run and verify:

```powershell
dotnet build .\InspectaBackend.slnx
dotnet test .\InspectaBackend.slnx
dotnet run --project .\Inspecta.Api\Inspecta.Api.csproj
```

The local database is `Inspecta.Api/inspecta.db`. EF Core migrations are applied on startup. Swagger is available at `/swagger` in Development.

## Initial API surface

All application endpoints are under `/api/v1`:

- Authentication: `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me`, `POST /auth/logout`.
- Staff: `POST /users`, `GET /users/{id}`, `PUT /users/{id}/profile`, `DELETE /users/{id}` (admin only).
- Clients: `GET/POST /clients`, `GET/DELETE /clients/{id}` (admin/coordinator).
- Equipment: `GET/POST /equipment`, `GET/PUT/DELETE /equipment/{id}`, `POST /equipment/{id}/inspections` (admin/coordinator).
- Requests: `GET/POST /requests`, `GET /requests/{id}`, `POST /requests/{id}/transitions`, `POST /requests/{id}/assignment`.
- Quotes: `GET/POST /requests/{id}/quotes`, `POST /requests/{id}/quotes/{quoteId}/send`, `POST /requests/{id}/quotes/{quoteId}/decision`.
- Certificates: `GET/PUT /requests/{id}/certificate`, `POST /requests/{id}/certificate/submit`, `POST /requests/{id}/certificate/review`.
- Timesheets: `GET/PUT /requests/{id}/timesheet`, `POST /requests/{id}/timesheet/review`.

See `Inspecta.Api.http` for sample requests. API responses use DTOs, not EF entities. Inspector visibility is limited to assigned work, and role/ownership checks are enforced in policy and application layers.

## Migration boundaries

- Firebase UIDs are stored verbatim and uniquely. Map legacy `supervisor` to `coordinator` during import.
- Firebase password hashes are incompatible with ASP.NET Identity hashes. Do not copy them into `PasswordHash`; existing staff need password-reset/onboarding before authentication cutover.
- Password-reset email delivery is not exposed yet: no sender, verified domain, or reset URL/deep-link contract was provided. Configure these first; a public endpoint must never return reset tokens.
- Firestore export/import and row-level reconciliation are not included. They need an approved export/schema and staged backup to preserve UIDs, historical IDs, certificates, and audit history.
- SQLite is for local development. Choose the production database/provider before deployment and generate/rehearse provider-specific migrations against a restored data export.
- PDF rendering/template snapshots, QR verification page, email ingestion/reply matching, file storage, complete audit/event history, and client mail integration still need their production contracts.