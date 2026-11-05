using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveCertificatePdfsToTheirOwnTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentLength",
                table: "IssuedCertificateFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IssuedCertificateContents",
                columns: table => new
                {
                    IssuedCertificateFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedCertificateContents", x => x.IssuedCertificateFileId);
                    table.ForeignKey(
                        name: "FK_IssuedCertificateContents_IssuedCertificateFiles_IssuedCert~",
                        column: x => x.IssuedCertificateFileId,
                        principalTable: "IssuedCertificateFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Whatever PDFs exist move across before the old column goes.
            migrationBuilder.Sql(@"INSERT INTO ""IssuedCertificateContents"" (""IssuedCertificateFileId"", ""Bytes"") SELECT ""Id"", ""Content"" FROM ""IssuedCertificateFiles"";");
            migrationBuilder.Sql(@"UPDATE ""IssuedCertificateFiles"" SET ""ContentLength"" = octet_length(""Content"");");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "IssuedCertificateFiles");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "IssuedCertificateFiles",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(@"UPDATE ""IssuedCertificateFiles"" f SET ""Content"" = c.""Bytes"" FROM ""IssuedCertificateContents"" c WHERE c.""IssuedCertificateFileId"" = f.""Id"";");

            migrationBuilder.DropTable(
                name: "IssuedCertificateContents");

            migrationBuilder.DropColumn(
                name: "ContentLength",
                table: "IssuedCertificateFiles");
        }
    }
}
