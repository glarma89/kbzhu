using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class RecipeVersionIngredientConfiguration
    : IEntityTypeConfiguration<RecipeVersionIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeVersionIngredient> builder)
    {
        builder.ToTable("RecipeVersionIngredients");
        builder.HasKey(ingredient => new
        {
            ingredient.RecipeId,
            ingredient.RecipeVersion,
            ingredient.FoodProductId
        });
        builder.Property(ingredient => ingredient.WeightGrams)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.WeightScale).IsRequired();
        builder.OwnsOne(ingredient => ingredient.NutritionPer100gSnapshot, nutrition =>
        {
            nutrition.Property(values => values.Calories).HasColumnName("CaloriesPer100gSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.ProteinGrams).HasColumnName("ProteinPer100gSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.FatGrams).HasColumnName("FatPer100gSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.CarbohydrateGrams).HasColumnName("CarbohydratesPer100gSnapshot")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
        });
        builder.Navigation(ingredient => ingredient.NutritionPer100gSnapshot).IsRequired();
        builder.HasOne<FoodProduct>().WithMany().HasForeignKey(ingredient => ingredient.FoodProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
