using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Translation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToTranslationKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "TranslationKeys",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationKeys_Key_TenantId",
                table: "TranslationKeys",
                columns: new[] { "Key", "TenantId" },
                unique: true,
                filter: "\"IsArchived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationKeys_TenantId",
                table: "TranslationKeys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TranslationKeys_Key_TenantId",
                table: "TranslationKeys");

            migrationBuilder.DropIndex(
                name: "IX_TranslationKeys_TenantId",
                table: "TranslationKeys");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TranslationKeys");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys",
                column: "Key",
                unique: true,
                filter: "\"IsArchived\" = false");
        }
    }
}
