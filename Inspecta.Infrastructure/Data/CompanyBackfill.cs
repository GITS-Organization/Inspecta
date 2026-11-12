namespace Inspecta.Infrastructure.Data;

/// <summary>
/// The data step of the migration that introduced companies. Before it, a company was its technical manager: the manager's user id was
/// the key of every row of the company. Each technical manager becomes a company <b>with the same id</b>, so no row keyed by it has to
/// change, and the manager (and everyone else) is attached to it. Written in SQL both PostgreSQL and SQLite understand, so a test runs
/// exactly what production runs.
/// </summary>
public static class CompanyBackfill
{
    public const string InsertCompanies = """
        INSERT INTO "Companies" ("Id", "Name", "IsActive", "SubscriptionStatus", "SubscriptionEndDate", "DefaultReviewerId", "CreatedAt")
        SELECT u."Id",
               COALESCE(NULLIF(TRIM(p."Name"), ''), u."FullName"),
               (u."IsActive" AND NOT u."IsDeleted"),
               COALESCE(u."SubscriptionStatus", 'Expired'),
               u."SubscriptionEndDate",
               u."Id",
               u."CreatedAt"
        FROM "Users" u
        LEFT JOIN "CompanyProfiles" p ON p."ManagerId" = u."Id"
        WHERE u."Role" = 'TechnicalManager'
          AND NOT EXISTS (SELECT 1 FROM "Companies" c WHERE c."Id" = u."Id")
        """;

    /// <summary>A technical manager now belongs to its company too (before, its own id was the company id and the column was empty).</summary>
    public const string AttachManagersToTheirCompany = """
        UPDATE "Users" SET "ManagerId" = "Id" WHERE "Role" = 'TechnicalManager' AND "ManagerId" IS NULL
        """;
}
