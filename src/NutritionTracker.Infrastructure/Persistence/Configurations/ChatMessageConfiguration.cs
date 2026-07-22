using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Chat;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Role).HasConversion<string>().IsRequired();
        builder.Property(message => message.Content).IsRequired();
        builder.Property(message => message.CreatedAtUtc).IsRequired();
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
