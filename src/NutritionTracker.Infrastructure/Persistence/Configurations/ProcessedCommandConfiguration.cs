using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedCommandConfiguration : IEntityTypeConfiguration<ProcessedCommand>
{
    public void Configure(EntityTypeBuilder<ProcessedCommand> builder)
    {
        builder.ToTable("ProcessedCommands");
        builder.HasKey(command => command.Id);
        builder.HasIndex(command => new { command.UserId, command.IdempotencyKey }).IsUnique();
        builder.Property(command => command.IdempotencyKey).IsRequired();
        builder.Property(command => command.CommandType).IsRequired();
        builder.Property(command => command.CreatedAtUtc).IsRequired();
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(command => command.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
