using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class FoodProductConfiguration : IEntityTypeConfiguration<FoodProduct>
{
    public void Configure(EntityTypeBuilder<FoodProduct> builder)
    {
        builder.ToTable("FoodProducts");
        builder.HasKey(product => product.Id);
        builder.HasIndex(product => product.NormalizedName);
        builder.Property(product => product.Name).IsRequired();
        builder.Property(product => product.NormalizedName).IsRequired();
        builder.Property(product => product.Brand);
        builder.Property(product => product.Source).IsRequired();
        builder.Property(product => product.IsVerified).IsRequired();
        builder.Property(product => product.CreatedAtUtc).IsRequired();
        builder.Property(product => product.UpdatedAtUtc).IsRequired();
        builder.Property(product => product.FiberPer100g)
            .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale);

        builder.OwnsOne(product => product.NutritionPer100g, nutrition =>
        {
            nutrition.Property(values => values.Calories).HasColumnName("CaloriesPer100g")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.ProteinGrams).HasColumnName("ProteinPer100g")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.FatGrams).HasColumnName("FatPer100g")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.CarbohydrateGrams).HasColumnName("CarbohydratesPer100g")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
        });
        builder.Navigation(product => product.NutritionPer100g).IsRequired();

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(product => product.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
