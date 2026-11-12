# Engineering rules

These rules keep one API safe for many companies (tenants: a technical manager and their team). A rule that is not a test is a wish, so each one below names the test that enforces it and says what to do when that test fails. The tests live in `Inspecta.Domain.Tests`; run them all with `dotnet test Inspecta.Domain.Tests`.

## 1. Layers only look inward
Api and Infrastructure know Application; Application knows Domain; Domain knows nothing of ours and no framework. Controllers talk to services, never to repositories or the database. Entities change through methods, not public setters.
- Enforced by: `ArchitectureTests`.
- When it fails: move the code to the layer it belongs to; do not loosen the test.

## 2. Every endpoint is protected, or anonymous on purpose
A controller action without `[Authorize]` is closed by default. The short list of public actions (sign-in, token refresh, logos, the public certificate check, the Microsoft sign-in callback) is written in the test.
- Enforced by: `ApiAuthorizationGuardTests`.
- When it fails: add the right policy. Only a truly public page goes on the list, with the reason in the pull request.
- Access is a **permission**: every action says `[RequirePermission(Permissions.X)]` and `RolePermissions` (one table, `Inspecta.Application/Authorization`) says which roles have it. Never write a policy name or a role list on an endpoint, and never compare a role string in new code: ask `RolePermissions.Has(role, permission)`. To change what a role may do, change its line in the table and the frozen expectation in `PermissionGuardTests` in the same commit.
- Enforced by: `PermissionGuardTests` (one access rule per action, no class-level or old-style attributes, every permission real and used, the policies follow the table, and the frozen list of what each endpoint allows).

## 3. A tenant-owned entity has a tenant query filter
An entity with `ManagerId` or `TenantId` has an EF query filter that uses it, so a forgotten `Where` cannot show one company's rows to another. The exceptions (`AppUser`, `CertificateCounter`, `CompanyProfile`, `CoordinatorEmailConfig`) are listed in the test, each with the reason it is safe.
- Enforced by: `TenantGuardTests`.
- When it fails: give the new entity a filter like the others in `InspectaDbContext`. Add it to the exceptions only if it is read strictly by manager id and the reason is written down.
- Adding an entity: tenant column, filter, a row in the isolation matrix (rule 5), then the migration.

## 4. Switching the filter off is a registered act
`IgnoreQueryFilters()` removes the company separation for one query. It is needed for the mail sync (it reads every team), for sign-in (it finds the user before a tenant is known) and a few public reads. Each file and its count is in the register.
- Enforced by: `FilterBypassGuardTests`.
- When it fails: scope the query by manager id instead. If the bypass is needed, add it to the register and explain it in the pull request, so a reviewer sees it.

## 5. One company cannot reach another's records
For each resource that is addressed by id (clients, client mail rules, requests, quotes, certificate files, team members), a manager of company B gets 404 or 403 on every read, change and delete of company A's record, and A's records never appear in B's lists.
- Enforced by: `CrossTenantApiTests`.
- When it fails: it is a real leak. Fix the service or repository; do not touch the test.
- Adding a resource with ids: add its test here in the same pull request.

## 6. No fact that matters lives only in one process's memory
A second API instance, or a restart, must not break correctness. What lives in memory today (login lock-out, send locks, import retry counters, the per-team promotion lock, the Microsoft token cache and its gates) is listed with what each needs before the API runs as two instances.
- Enforced by: `ProcessLocalStateGuardTests`.
- When it fails: keep the state in the database or a shared store. A cache that is only a speed-up and can be lost safely may be listed, with that said.
- Before running more than one instance: move every entry on that list, run migrations as a deploy step instead of at start-up, and move the mail sync out of the API process.

## 7. Migrations add; they do not destroy
A push to the backend branch migrates the production database at start-up. A migration must not drop or rename a table or column, make a column required, or shorten one in a single step. The two historic exceptions are listed.
- Enforced by: `MigrationSafetyGuardTests`.
- When it fails: do it over two releases. First add the new column or table and move the data; remove the old one in a later migration once nothing reads it. A deliberate exception is added to the list with the reason.

## 8. Secrets stay secret
Passwords, refresh tokens and mailbox passwords are stored encrypted (`ISecretProtector`), never logged, never returned. A Microsoft error text is not passed on to a caller.
- Enforced by: `MailboxAuthTests`, `MicrosoftConnectionHttpTests`, `PasswordHttpTests` for the paths that exist today; review for new ones.

## 9. Lists are paged
A list endpoint takes `page` and `pageSize` with a maximum, so one large company cannot make an answer unbounded.
- Enforced by: `ListPagingHttpTests`.

## 10. Tests first, and a push is a release
New behaviour starts with a test that fails. Pushing to the backend branch deploys to production and migrates the database, so push only when the whole suite is green and say in the pull request when a migration is in it.

## Known limits for many companies (not yet rules)
- All companies share one database; there is no per-company quota, so a large one can slow the others.
- Certificate PDFs are stored in the database (their own table); object storage comes when the data grows.
- There is no audit log of who changed what, and no company-wide data export or removal.
- Mailboxes are not yet bound to a company domain beyond the Outlook connection check.
