using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nasheed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorLegalComplianceOwnedType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SongIngestionJobs_SongId",
                table: "SongIngestionJobs");

            migrationBuilder.CreateIndex(
                name: "IX_SongIngestionJobs_ActiveJobUnique",
                table: "SongIngestionJobs",
                columns: new[] { "SongId", "JobType" },
                unique: true,
                filter: "\"JobStatus\" IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SongIngestionJobs_ActiveJobUnique",
                table: "SongIngestionJobs");

            migrationBuilder.CreateIndex(
                name: "IX_SongIngestionJobs_SongId",
                table: "SongIngestionJobs",
                column: "SongId");
        }
    }
}
