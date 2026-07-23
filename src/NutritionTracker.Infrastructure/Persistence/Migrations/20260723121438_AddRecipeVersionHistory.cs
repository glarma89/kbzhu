using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeVersionHistory : Migration
    {
        private static readonly string[] RecipeVersionKeyColumns = ["RecipeId", "Version"];
        private static readonly string[] RecipeVersionForeignKeyColumns = ["RecipeId", "RecipeVersion"];
        private static readonly string[] RecipeSearchColumns = ["UserId", "NormalizedName"];
        private static readonly string[] LegacyRecipeSearchColumns = ["UserId", "Name"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_UserId_Name",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_MealItems_RecipeId",
                table: "MealItems");

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Recipes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveSource",
                table: "Recipes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "Recipes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TotalPreparedWeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    ChangeReason = table.Column<string>(type: "TEXT", nullable: true),
                    ChangeSource = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => new { x.RecipeId, x.Version });
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersionIngredients",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    CaloriesPer100gSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProteinPer100gSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FatPer100gSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CarbohydratesPer100gSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersionIngredients", x => new { x.RecipeId, x.RecipeVersion, x.FoodProductId });
                    table.ForeignKey(
                        name: "FK_RecipeVersionIngredients_FoodProducts_FoodProductId",
                        column: x => x.FoodProductId,
                        principalTable: "FoodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeVersionIngredients_RecipeVersions_RecipeId_RecipeVersion",
                        columns: x => new { x.RecipeId, x.RecipeVersion },
                        principalTable: "RecipeVersions",
                        principalColumns: RecipeVersionKeyColumns,
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                "UPDATE \"Recipes\" SET \"NormalizedName\" = UPPER(TRIM(\"Name\"));");

            migrationBuilder.Sql(
                """
                INSERT INTO "RecipeVersions"
                    ("RecipeId", "Version", "Name", "Description", "TotalPreparedWeightGrams",
                     "ChangeReason", "ChangeSource", "ChangedAtUtc")
                SELECT "Id", "Version", "Name", "Description", "TotalPreparedWeightGrams",
                       'Imported from pre-version-history schema', 'Migration', "UpdatedAtUtc"
                FROM "Recipes";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "RecipeVersionIngredients"
                    ("RecipeId", "RecipeVersion", "FoodProductId", "WeightGrams",
                     "CaloriesPer100gSnapshot", "ProteinPer100gSnapshot",
                     "FatPer100gSnapshot", "CarbohydratesPer100gSnapshot")
                SELECT ingredient."RecipeId", recipe."Version", ingredient."FoodProductId",
                       ingredient."WeightGrams", product."CaloriesPer100g",
                       product."ProteinPer100g", product."FatPer100g",
                       product."CarbohydratesPer100g"
                FROM "RecipeIngredients" AS ingredient
                INNER JOIN "Recipes" AS recipe ON recipe."Id" = ingredient."RecipeId"
                INNER JOIN "FoodProducts" AS product ON product."Id" = ingredient."FoodProductId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId_NormalizedName",
                table: "Recipes",
                columns: RecipeSearchColumns);

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_RecipeId_RecipeVersion",
                table: "MealItems",
                columns: RecipeVersionForeignKeyColumns);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersionIngredients_FoodProductId",
                table: "RecipeVersionIngredients",
                column: "FoodProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_MealItems_RecipeVersions_RecipeId_RecipeVersion",
                table: "MealItems",
                columns: RecipeVersionForeignKeyColumns,
                principalTable: "RecipeVersions",
                principalColumns: RecipeVersionKeyColumns,
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealItems_RecipeVersions_RecipeId_RecipeVersion",
                table: "MealItems");

            migrationBuilder.DropTable(
                name: "RecipeVersionIngredients");

            migrationBuilder.DropTable(
                name: "RecipeVersions");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_UserId_NormalizedName",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_MealItems_RecipeId_RecipeVersion",
                table: "MealItems");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ArchiveSource",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Recipes");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId_Name",
                table: "Recipes",
                columns: LegacyRecipeSearchColumns);

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_RecipeId",
                table: "MealItems",
                column: "RecipeId");
        }
    }
}
