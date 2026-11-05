using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIssuedCertificateNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unique numbers and codes are a backstop for the checks the services already make. If the existing data still holds a
            // duplicate, the index is skipped with a warning (the old non-unique index stays) so that the start-up migration never blocks a deploy.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""IssuedCertificateFiles"" GROUP BY ""ManagerId"", ""CertificateNumber"" HAVING COUNT(*) > 1) THEN
        RAISE WARNING 'Duplicate certificate numbers exist: unique index IX_IssuedCertificateFiles_ManagerId_CertificateNumber skipped';
    ELSE
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_IssuedCertificateFiles_ManagerId_CertificateNumber"" ON ""IssuedCertificateFiles"" (""ManagerId"", ""CertificateNumber"");
    END IF;
    IF EXISTS (SELECT 1 FROM ""IssuedCertificateFiles"" GROUP BY ""VerificationCode"" HAVING COUNT(*) > 1) THEN
        RAISE WARNING 'Duplicate verification codes exist: unique index IX_IssuedCertificateFiles_VerificationCode skipped';
    ELSE
        DROP INDEX IF EXISTS ""IX_IssuedCertificateFiles_VerificationCode"";
        CREATE UNIQUE INDEX ""IX_IssuedCertificateFiles_VerificationCode"" ON ""IssuedCertificateFiles"" (""VerificationCode"");
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_IssuedCertificateFiles_ManagerId_CertificateNumber\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_IssuedCertificateFiles_VerificationCode\";");
            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_VerificationCode",
                table: "IssuedCertificateFiles",
                column: "VerificationCode");
        }
    }
}
