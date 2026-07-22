using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("Meals");
        builder.HasKey(meal => meal.Id);
        builder.HasIndex(meal => new { meal.UserId, meal.OccurredAt });
        builder.Property(meal => meal.OccurredAt).IsRequired();
        builder.Property(meal => meal.MealType).HasConversion<string>().IsRequired();
        builder.Property(meal => meal.Notes);
        builder.Property(meal => meal.CreatedAtUtc).IsRequired();
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(meal => meal.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
