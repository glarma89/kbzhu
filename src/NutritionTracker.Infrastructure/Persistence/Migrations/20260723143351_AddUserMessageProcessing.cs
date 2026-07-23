using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMessageProcessing : Migration
    {
        private static readonly string[] DeliveryKeyIndexColumns = ["UserId", "DeliveryKey"];
        private static readonly string[] IdempotencyKeyIndexColumns = ["UserId", "IdempotencyKey"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMessageProcessings",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeliveryKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    InterpretationJson = table.Column<string>(type: "TEXT", nullable: true),
                    PendingQuestion = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ClarificationResponse = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ToolName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ToolArgumentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExecutionResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RetryFromState = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResponseDeliveredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMessageProcessings", x => x.MessageId);
                    table.CheckConstraint("CK_UserMessageProcessings_CompletedHasResult", "\"State\" <> 'Completed' OR \"ExecutionResultJson\" IS NOT NULL");
                    table.CheckConstraint("CK_UserMessageProcessings_OperationHasTool", "\"State\" NOT IN ('AwaitingConfirmation', 'Executing') OR (\"ToolName\" IS NOT NULL AND \"ToolArgumentsJson\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserMessageProcessings_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMessageProcessings_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMessageProcessings_UserId_DeliveryKey",
                table: "UserMessageProcessings",
                columns: DeliveryKeyIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMessageProcessings_UserId_IdempotencyKey",
                table: "UserMessageProcessings",
                columns: IdempotencyKeyIndexColumns,
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMessageProcessings");
        }
    }
}
