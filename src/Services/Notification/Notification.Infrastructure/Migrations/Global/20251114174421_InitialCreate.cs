using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Notification.Infrastructure.Migrations.Global
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryType = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    QueueStatus = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotificationId = table.Column<int>(type: "integer", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationQueue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_Cleanup",
                table: "NotificationQueue",
                columns: new[] { "QueueStatus", "LastModified" },
                filter: "\"QueueStatus\" IN (2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_Expiration",
                table: "NotificationQueue",
                columns: new[] { "ExpiresAt", "QueueStatus" },
                filter: "\"QueueStatus\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_Processing",
                table: "NotificationQueue",
                columns: new[] { "QueueStatus", "ExpiresAt", "NextRetryAt", "Priority", "Created" },
                filter: "\"QueueStatus\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_Tenant",
                table: "NotificationQueue",
                columns: new[] { "TenantId", "QueueStatus", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_User",
                table: "NotificationQueue",
                columns: new[] { "UserId", "QueueStatus", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationQueue");
        }
    }
}
