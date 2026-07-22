using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] MealUserOccurredAtIndexColumns = ["UserId", "OccurredAt"];
        private static readonly string[] ProcessedCommandIdempotencyIndexColumns = ["UserId", "IdempotencyKey"];
        private static readonly string[] RecipeUserNameIndexColumns = ["UserId", "Name"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FoodProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    CaloriesPer100g = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProteinPer100g = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FatPer100g = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CarbohydratesPer100g = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FiberPer100g = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodProducts_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MealType = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meals_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NutritionTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Calories = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FatGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CarbohydrateGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionTargets_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    CommandType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedCommands_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TotalPreparedWeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FoodProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    RecipeVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    CaloriesSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProteinSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FatSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CarbohydratesSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealItems", x => x.Id);
                    table.CheckConstraint("CK_MealItems_ExactlyOneSource", "(\"FoodProductId\" IS NOT NULL AND \"RecipeId\" IS NULL) OR (\"FoodProductId\" IS NULL AND \"RecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MealItems_ChatMessages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MealItems_FoodProducts_FoodProductId",
                        column: x => x.FoodProductId,
                        principalTable: "FoodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealItems_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealItems_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FoodProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_FoodProducts_FoodProductId",
                        column: x => x.FoodProductId,
                        principalTable: "FoodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodProducts_NormalizedName",
                table: "FoodProducts",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_FoodProducts_UserId",
                table: "FoodProducts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_FoodProductId",
                table: "MealItems",
                column: "FoodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_MealId",
                table: "MealItems",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_RecipeId",
                table: "MealItems",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_MealItems_SourceMessageId",
                table: "MealItems",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_UserId_OccurredAt",
                table: "Meals",
                columns: MealUserOccurredAtIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_NutritionTargets_UserId",
                table: "NutritionTargets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedCommands_UserId_IdempotencyKey",
                table: "ProcessedCommands",
                columns: ProcessedCommandIdempotencyIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_FoodProductId",
                table: "RecipeIngredients",
                column: "FoodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId_Name",
                table: "Recipes",
                columns: RecipeUserNameIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealItems");

            migrationBuilder.DropTable(
                name: "NutritionTargets");

            migrationBuilder.DropTable(
                name: "ProcessedCommands");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "FoodProducts");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
