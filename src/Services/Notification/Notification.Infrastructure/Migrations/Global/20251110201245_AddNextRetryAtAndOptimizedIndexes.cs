using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations.Global
{
    /// <inheritdoc />
    public partial class AddNextRetryAtAndOptimizedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_ExpiresAt",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_Status_Created",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_TenantId",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_UserId",
                table: "NotificationQueue");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "NotificationQueue",
                type: "timestamp with time zone",
                nullable: true);

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
            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_Cleanup",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_Expiration",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_Processing",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_Tenant",
                table: "NotificationQueue");

            migrationBuilder.DropIndex(
                name: "IX_NotificationQueue_User",
                table: "NotificationQueue");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "NotificationQueue");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_ExpiresAt",
                table: "NotificationQueue",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_Status_Created",
                table: "NotificationQueue",
                columns: new[] { "QueueStatus", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_TenantId",
                table: "NotificationQueue",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueue_UserId",
                table: "NotificationQueue",
                column: "UserId");
        }
    }
}
