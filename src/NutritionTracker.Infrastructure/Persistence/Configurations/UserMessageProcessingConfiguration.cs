using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Chat;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserMessageProcessingConfiguration
    : IEntityTypeConfiguration<UserMessageProcessing>
{
    public void Configure(EntityTypeBuilder<UserMessageProcessing> builder)
    {
        builder.ToTable(
            "UserMessageProcessings",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_UserMessageProcessings_CompletedHasResult",
                    "\"State\" <> 'Completed' OR \"ExecutionResultJson\" IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_UserMessageProcessings_OperationHasTool",
                    "\"State\" NOT IN ('AwaitingConfirmation', 'Executing') OR " +
                    "(\"ToolName\" IS NOT NULL AND \"ToolArgumentsJson\" IS NOT NULL)");
            });
        builder.HasKey(processing => processing.MessageId);
        builder.Property(processing => processing.DeliveryKey).HasMaxLength(200).IsRequired();
        builder.Property(processing => processing.State).HasConversion<string>().IsRequired();
        builder.Property(processing => processing.InterpretationJson);
        builder.Property(processing => processing.PendingQuestion).HasMaxLength(2_000);
        builder.Property(processing => processing.ClarificationResponse).HasMaxLength(2_000);
        builder.Property(processing => processing.ToolName).HasMaxLength(100);
        builder.Property(processing => processing.ToolArgumentsJson);
        builder.Property(processing => processing.IdempotencyKey).HasMaxLength(200);
        builder.Property(processing => processing.ExecutionResultJson);
        builder.Property(processing => processing.FailureCode).HasMaxLength(100);
        builder.Property(processing => processing.FailureMessage).HasMaxLength(2_000);
        builder.Property(processing => processing.RetryFromState).HasConversion<string>();
        builder.Property(processing => processing.CreatedAtUtc).IsRequired();
        builder.Property(processing => processing.UpdatedAtUtc).IsRequired();
        builder.Property(processing => processing.CompletedAtUtc);
        builder.Property(processing => processing.ConfirmedAtUtc);
        builder.Property(processing => processing.ResponseDeliveredAtUtc);
        builder.HasIndex(processing => new { processing.UserId, processing.DeliveryKey }).IsUnique();
        builder.HasIndex(processing => new { processing.UserId, processing.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasOne<ChatMessage>().WithOne().HasForeignKey<UserMessageProcessing>(
            processing => processing.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(processing => processing.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
