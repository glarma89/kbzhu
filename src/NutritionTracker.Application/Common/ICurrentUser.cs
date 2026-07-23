namespace NutritionTracker.Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }
}

public static class UserIdentityClaimTypes
{
    public const string UserId = "sub";
}
