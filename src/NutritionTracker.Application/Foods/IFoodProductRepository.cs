using NutritionTracker.Domain.Foods;

namespace NutritionTracker.Application.Foods;

public interface IFoodProductRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(FoodProduct product, CancellationToken cancellationToken);

    Task<FoodProduct?> GetVisibleByIdAsync(
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken);

    Task<FoodProduct?> GetOwnedByIdAsync(
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FoodProduct>> SearchAsync(
        Guid? userId,
        string? normalizedQuery,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FoodProduct>> FindCandidatesByNameAsync(
        Guid? userId,
        string normalizedName,
        int limit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
