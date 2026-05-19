using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Category.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUriAndFileIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "icon_url",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "categories");

            migrationBuilder.AddColumn<int>(
                name: "icon_file_id",
                table: "categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon_name",
                table: "categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_file_id",
                table: "categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uri",
                table: "categories",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            // Backfill uri from slug so existing rows have unique values before the index is created.
            // slug is already unique per category, so this prevents the 23505 duplicate-value error.
            migrationBuilder.Sql("UPDATE categories SET uri = slug WHERE uri = ''");

            migrationBuilder.CreateIndex(
                name: "ix_categories_uri_unique",
                table: "categories",
                column: "uri",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categories_uri_unique",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "icon_file_id",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "icon_name",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "image_file_id",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "uri",
                table: "categories");

            migrationBuilder.AddColumn<string>(
                name: "icon_url",
                table: "categories",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "categories",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
