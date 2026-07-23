using NutritionTracker.Application.Common;

namespace NutritionTracker.Api.Authentication;

internal sealed class ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(
                UserIdentityClaimTypes.UserId)?.Value;
            return Guid.TryParse(value, out var userId) && userId != Guid.Empty
                ? userId
                : throw new InvalidOperationException(
                    "The authenticated principal has no valid user identifier claim.");
        }
    }
}
