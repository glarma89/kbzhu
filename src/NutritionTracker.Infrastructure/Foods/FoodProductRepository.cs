using Microsoft.EntityFrameworkCore;
using NutritionTracker.Application.Foods;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Foods;

internal sealed class FoodProductRepository(NutritionDbContext context) : IFoodProductRepository
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return context.UserProfiles.AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task AddAsync(FoodProduct product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        await context.FoodProducts.AddAsync(product, cancellationToken);
    }

    public Task<FoodProduct?> GetVisibleByIdAsync(
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return context.FoodProducts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == id &&
                    (product.UserId == null || product.UserId == userId),
                cancellationToken);
    }

    public Task<FoodProduct?> GetOwnedByIdAsync(
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return context.FoodProducts.SingleOrDefaultAsync(
            product => product.Id == id && product.UserId == userId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FoodProduct>> SearchAsync(
        Guid? userId,
        string? normalizedQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = VisibleProducts(userId);
        if (normalizedQuery is not null)
        {
            query = query.Where(product => product.NormalizedName.Contains(normalizedQuery));
            query = OrderByRelevance(query, userId, normalizedQuery);
        }
        else
        {
            query = query
                .OrderBy(product => product.UserId == userId ? 0 : 1)
                .ThenBy(product => product.Name);
        }

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FoodProduct>> FindCandidatesByNameAsync(
        Guid? userId,
        string normalizedName,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = VisibleProducts(userId)
            .Where(product => product.NormalizedName.Contains(normalizedName));
        return await OrderByRelevance(query, userId, normalizedName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<FoodProduct> VisibleProducts(Guid? userId)
    {
        return context.FoodProducts
            .AsNoTracking()
            .Where(product => product.UserId == null || product.UserId == userId);
    }

    private static IOrderedQueryable<FoodProduct> OrderByRelevance(
        IQueryable<FoodProduct> query,
        Guid? userId,
        string normalizedName)
    {
        return query
            .OrderBy(product => product.UserId == userId ? 0 : 1)
            .ThenBy(product => product.NormalizedName == normalizedName
                ? 0
                : product.NormalizedName.StartsWith(normalizedName) ? 1 : 2)
            .ThenByDescending(product => product.IsVerified)
            .ThenBy(product => product.Name);
    }
}
