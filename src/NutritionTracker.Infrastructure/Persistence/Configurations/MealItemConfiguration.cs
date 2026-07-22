using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Chat;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class MealItemConfiguration : IEntityTypeConfiguration<MealItem>
{
    public void Configure(EntityTypeBuilder<MealItem> builder)
    {
        builder.ToTable(
            "MealItems",
            table => table.HasCheckConstraint(
                "CK_MealItems_ExactlyOneSource",
                "(\"FoodProductId\" IS NOT NULL AND \"RecipeId\" IS NULL) OR " +
                "(\"FoodProductId\" IS NULL AND \"RecipeId\" IS NOT NULL)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.WeightGrams)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.WeightScale).IsRequired();
        builder.Property(item => item.RecipeVersion);

        builder.OwnsOne(item => item.NutritionSnapshot, nutrition =>
        {
            nutrition.Property(values => values.Calories).HasColumnName("CaloriesSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.ProteinGrams).HasColumnName("ProteinSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.FatGrams).HasColumnName("FatSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.CarbohydrateGrams).HasColumnName("CarbohydratesSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
        });
        builder.Navigation(item => item.NutritionSnapshot).IsRequired();

        builder.HasOne<Meal>().WithMany().HasForeignKey(item => item.MealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FoodProduct>().WithMany().HasForeignKey(item => item.FoodProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Recipe>().WithMany().HasForeignKey(item => item.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChatMessage>().WithMany().HasForeignKey(item => item.SourceMessageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
