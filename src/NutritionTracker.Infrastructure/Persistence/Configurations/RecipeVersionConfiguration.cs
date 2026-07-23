using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> builder)
    {
        builder.ToTable("RecipeVersions");
        builder.HasKey(version => new { version.RecipeId, version.Version });
        builder.Property(version => version.Name).IsRequired();
        builder.Property(version => version.Description);
        builder.Property(version => version.TotalPreparedWeightGrams)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.WeightScale);
        builder.Property(version => version.ChangeReason);
        builder.Property(version => version.ChangeSource).IsRequired();
        builder.Property(version => version.ChangedAtUtc).IsRequired();

        builder.HasOne<Recipe>().WithMany(recipe => recipe.Versions)
            .HasForeignKey(version => version.RecipeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(version => version.Ingredients).WithOne()
            .HasForeignKey(ingredient => new { ingredient.RecipeId, ingredient.RecipeVersion })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(version => version.Ingredients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
