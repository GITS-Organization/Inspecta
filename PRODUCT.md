# Inspecta API product scope

Inspecta manages the inspection lifecycle for client equipment and items through an ASP.NET Core REST API. Separately hosted web or mobile clients consume the versioned `/api/v1` endpoints.

The API supports:

- Company provisioning and role-based team management for Admins and Technical Managers.
- Client records, contacts, equipment/items, item metadata, and inspection history.
- Inspection request intake, workflow transitions, inspector assignment, and scheduling.
- Quote revisions and client decisions.
- Inspection certificates and technical review.

Authentication uses API-managed accounts and JWT access/refresh tokens. The backend does not require a frontend framework or an external identity provider.
