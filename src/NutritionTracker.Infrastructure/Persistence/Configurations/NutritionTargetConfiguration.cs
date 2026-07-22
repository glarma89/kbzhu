using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Infrastructure.Persistence.Configurations;

internal sealed class NutritionTargetConfiguration : IEntityTypeConfiguration<NutritionTarget>
{
    public void Configure(EntityTypeBuilder<NutritionTarget> builder)
    {
        builder.ToTable("NutritionTargets");
        builder.HasKey(target => target.Id);
        builder.Property(target => target.ValidFrom).IsRequired();

        builder.OwnsOne(target => target.NutritionValues, nutrition =>
        {
            nutrition.Property(values => values.Calories).HasColumnName("Calories")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.ProteinGrams).HasColumnName("ProteinGrams")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.FatGrams).HasColumnName("FatGrams")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
            nutrition.Property(values => values.CarbohydrateGrams).HasColumnName("CarbohydrateGrams")
                .HasPrecision(PersistenceConstants.NumericPrecision, PersistenceConstants.NutritionScale).IsRequired();
        });
        builder.Navigation(target => target.NutritionValues).IsRequired();

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(target => target.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
