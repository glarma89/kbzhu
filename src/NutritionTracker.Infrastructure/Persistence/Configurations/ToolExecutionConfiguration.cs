using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("ToolExecutions");
        builder.HasKey(execution => execution.Id);
        builder.HasIndex(execution => new { execution.UserId, execution.IdempotencyKey }).IsUnique();
        builder.Property(execution => execution.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(execution => execution.ToolName).HasMaxLength(100).IsRequired();
        builder.Property(execution => execution.ArgumentsHash).HasMaxLength(64).IsRequired();
        builder.Property(execution => execution.ResultJson).IsRequired();
        builder.Property(execution => execution.CreatedAtUtc).IsRequired();
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(execution => execution.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
