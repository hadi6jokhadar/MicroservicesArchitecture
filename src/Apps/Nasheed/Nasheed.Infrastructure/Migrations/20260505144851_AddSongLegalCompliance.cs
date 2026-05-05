using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nasheed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongLegalCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentSafetyFlag",
                table: "Songs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopyrightRiskLevel",
                table: "Songs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskReason",
                table: "Songs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentSafetyFlag",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "CopyrightRiskLevel",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "RiskReason",
                table: "Songs");
        }
    }
}
