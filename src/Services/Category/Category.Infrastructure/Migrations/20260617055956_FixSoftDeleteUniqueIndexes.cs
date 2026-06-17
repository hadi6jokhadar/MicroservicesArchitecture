using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Category.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSoftDeleteUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categories_slug_unique",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_categories_uri_unique",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug_unique",
                table: "categories",
                column: "slug",
                unique: true,
                filter: "\"IsArchived\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_categories_uri_unique",
                table: "categories",
                column: "uri",
                unique: true,
                filter: "\"IsArchived\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categories_slug_unique",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_categories_uri_unique",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug_unique",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_uri_unique",
                table: "categories",
                column: "uri",
                unique: true);
        }
    }
}
