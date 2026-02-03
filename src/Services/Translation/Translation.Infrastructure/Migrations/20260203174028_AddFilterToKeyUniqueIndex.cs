using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Translation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterToKeyUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys",
                column: "Key",
                unique: true,
                filter: "\"IsArchived\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationKeys_Key",
                table: "TranslationKeys",
                column: "Key",
                unique: true);
        }
    }
}
