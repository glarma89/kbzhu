using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Users;

public sealed class UserProfile
{
    public UserProfile(Guid id, string displayName, string timeZone, DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        DisplayName = DomainGuard.RequiredText(displayName, nameof(displayName));
        TimeZone = DomainGuard.RequiredText(timeZone, nameof(timeZone));
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string TimeZone { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
