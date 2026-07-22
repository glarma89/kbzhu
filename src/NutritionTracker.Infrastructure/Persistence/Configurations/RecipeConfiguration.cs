using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");
        builder.HasKey(recipe => recipe.Id);
        builder.HasIndex(recipe => new { recipe.UserId, recipe.Name });
        builder.Property(recipe => recipe.Name).IsRequired();
        builder.Property(recipe => recipe.Description);
        builder.Property(recipe => recipe.TotalPreparedWeightGrams)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.WeightScale);
        builder.Property(recipe => recipe.Version).IsRequired();
        builder.Property(recipe => recipe.IsArchived).IsRequired();
        builder.Property(recipe => recipe.CreatedAtUtc).IsRequired();
        builder.Property(recipe => recipe.UpdatedAtUtc).IsRequired();

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(recipe => recipe.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(recipe => recipe.Ingredients).WithOne()
            .HasForeignKey(ingredient => ingredient.RecipeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(recipe => recipe.Ingredients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
