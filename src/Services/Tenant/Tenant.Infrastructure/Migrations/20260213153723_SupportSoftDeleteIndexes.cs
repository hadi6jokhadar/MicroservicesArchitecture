using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportSoftDeleteIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings");

            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_UserId",
                table: "TenantSettings");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings",
                column: "TenantId",
                unique: true,
                filter: "\"IsArchived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_UserId",
                table: "TenantSettings",
                column: "UserId",
                unique: true,
                filter: "\"IsArchived\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings");

            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_UserId",
                table: "TenantSettings");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_UserId",
                table: "TenantSettings",
                column: "UserId",
                unique: true);
        }
    }
}
