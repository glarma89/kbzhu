using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Chat;

public sealed class ChatMessage
{
    public ChatMessage(
        Guid id,
        Guid userId,
        ChatRole role,
        string content,
        DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The chat role is invalid.");
        }

        Role = role;
        Content = DomainGuard.RequiredText(content, nameof(content));
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public ChatRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
