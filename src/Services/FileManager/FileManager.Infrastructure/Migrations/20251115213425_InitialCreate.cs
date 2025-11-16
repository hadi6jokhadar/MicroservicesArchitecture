using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FileManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileManager",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Temp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileManager", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_Created",
                table: "FileManager",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_Group",
                table: "FileManager",
                column: "Group");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_IsArchived",
                table: "FileManager",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_Status",
                table: "FileManager",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_Temp",
                table: "FileManager",
                column: "Temp");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_Type",
                table: "FileManager",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_FileManager_UserId",
                table: "FileManager",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileManager");
        }
    }
}
