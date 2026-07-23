using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ResultDate",
                table: "ProcessedCommands",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "ResultEntityId",
                table: "ProcessedCommands",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            ConvertOccurredAtToUnixMilliseconds(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultDate",
                table: "ProcessedCommands");

            migrationBuilder.DropColumn(
                name: "ResultEntityId",
                table: "ProcessedCommands");

            ConvertOccurredAtToText(migrationBuilder);
        }

        private static void ConvertOccurredAtToUnixMilliseconds(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_Meals" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Meals" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "OccurredAt" INTEGER NOT NULL,
                    "MealType" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_Meals_UserProfiles_UserId" FOREIGN KEY ("UserId")
                        REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
                );
                INSERT INTO "ef_temp_Meals"
                    ("Id", "UserId", "OccurredAt", "MealType", "Notes", "CreatedAtUtc")
                SELECT "Id", "UserId",
                    CAST(ROUND((julianday("OccurredAt") - 2440587.5) * 86400000.0) AS INTEGER),
                    "MealType", "Notes", "CreatedAtUtc"
                FROM "Meals";
                DROP TABLE "Meals";
                ALTER TABLE "ef_temp_Meals" RENAME TO "Meals";
                CREATE INDEX "IX_Meals_UserId_OccurredAt" ON "Meals" ("UserId", "OccurredAt");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }

        private static void ConvertOccurredAtToText(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_Meals" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Meals" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "OccurredAt" TEXT NOT NULL,
                    "MealType" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_Meals_UserProfiles_UserId" FOREIGN KEY ("UserId")
                        REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
                );
                INSERT INTO "ef_temp_Meals"
                    ("Id", "UserId", "OccurredAt", "MealType", "Notes", "CreatedAtUtc")
                SELECT "Id", "UserId",
                    strftime('%Y-%m-%d %H:%M:%f+00:00', "OccurredAt" / 1000.0, 'unixepoch'),
                    "MealType", "Notes", "CreatedAtUtc"
                FROM "Meals";
                DROP TABLE "Meals";
                ALTER TABLE "ef_temp_Meals" RENAME TO "Meals";
                CREATE INDEX "IX_Meals_UserId_OccurredAt" ON "Meals" ("UserId", "OccurredAt");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
