using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nasheed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_EntityType_OccurredAt"" ON ""AuditLogs"" (""EntityType"" ASC, ""OccurredAt"" DESC)");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_TenantId_OccurredAt"" ON ""AuditLogs"" (""TenantId"" ASC, ""OccurredAt"" DESC)");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_UserId_OccurredAt"" ON ""AuditLogs"" (""UserId"" ASC, ""OccurredAt"" DESC)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AuditLogs_EntityType_OccurredAt""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AuditLogs_TenantId_OccurredAt""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AuditLogs_UserId_OccurredAt""");
        }
    }
}
