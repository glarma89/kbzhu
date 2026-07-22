using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.DisplayName).IsRequired();
        builder.Property(user => user.TimeZone).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
    }
}
