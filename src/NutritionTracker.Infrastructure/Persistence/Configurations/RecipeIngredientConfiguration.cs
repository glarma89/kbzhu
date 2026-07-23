using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients");
        builder.HasKey(ingredient => ingredient.Id);
        builder.Property(ingredient => ingredient.Id).ValueGeneratedNever();
        builder.Property(ingredient => ingredient.WeightGrams)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.WeightScale).IsRequired();
        builder.HasOne<FoodProduct>().WithMany().HasForeignKey(ingredient => ingredient.FoodProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
