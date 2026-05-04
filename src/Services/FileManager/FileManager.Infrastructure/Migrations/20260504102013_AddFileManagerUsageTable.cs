using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FileManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileManagerUsageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileManagerUsage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    UsageArea = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileManagerUsage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileManagerUsage_FileId",
                table: "FileManagerUsage",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileManagerUsage_FileId_UsageArea_RowId",
                table: "FileManagerUsage",
                columns: new[] { "FileId", "UsageArea", "RowId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileManagerUsage");
        }
    }
}
