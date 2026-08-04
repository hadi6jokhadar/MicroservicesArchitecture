using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nasheed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongLyricsVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LyricsVerified",
                table: "Songs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LyricsVerified",
                table: "Songs");
        }
    }
}
