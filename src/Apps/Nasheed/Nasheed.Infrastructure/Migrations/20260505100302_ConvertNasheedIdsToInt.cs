using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nasheed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertNasheedIdsToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Songs"
                ALTER COLUMN "FileId" TYPE integer
                USING "FileId"::integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "SongIngestionJobs"
                ALTER COLUMN "FileId" TYPE integer
                USING "FileId"::integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Ratings"
                ALTER COLUMN "UserId" TYPE integer
                USING "UserId"::integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "PlayLogs"
                ALTER COLUMN "UserId" TYPE integer
                USING "UserId"::integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Favorites"
                ALTER COLUMN "UserId" TYPE integer
                USING "UserId"::integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Artists"
                ALTER COLUMN "ImageFileId" TYPE integer
                USING NULLIF("ImageFileId", '')::integer;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Songs"
                ALTER COLUMN "FileId" TYPE character varying(100)
                USING "FileId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "SongIngestionJobs"
                ALTER COLUMN "FileId" TYPE character varying(100)
                USING "FileId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Ratings"
                ALTER COLUMN "UserId" TYPE character varying(100)
                USING "UserId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "PlayLogs"
                ALTER COLUMN "UserId" TYPE character varying(100)
                USING "UserId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Favorites"
                ALTER COLUMN "UserId" TYPE character varying(100)
                USING "UserId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Artists"
                ALTER COLUMN "ImageFileId" TYPE text
                USING "ImageFileId"::text;
                """);
        }
    }
}
