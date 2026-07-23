using Microsoft.EntityFrameworkCore;
using NutritionTracker.Application.Chat;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Chat;

internal sealed class UserMessageProcessingRepository(NutritionDbContext context)
    : IUserMessageProcessingRepository
{
    public async Task<StoredUserMessage?> GetByMessageIdAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var processing = await context.UserMessageProcessings.SingleOrDefaultAsync(
            item => item.MessageId == messageId && item.UserId == userId,
            cancellationToken);
        if (processing is null)
        {
            return null;
        }

        var message = await context.ChatMessages.AsNoTracking().SingleAsync(
            item => item.Id == processing.MessageId && item.UserId == userId,
            cancellationToken);
        return new StoredUserMessage(message, processing);
    }

    public async Task<StoredUserMessage?> GetByDeliveryKeyAsync(
        Guid userId,
        string deliveryKey,
        CancellationToken cancellationToken)
    {
        var processing = await context.UserMessageProcessings.SingleOrDefaultAsync(
            item => item.UserId == userId && item.DeliveryKey == deliveryKey,
            cancellationToken);
        if (processing is null)
        {
            return null;
        }

        var message = await context.ChatMessages.AsNoTracking().SingleAsync(
            item => item.Id == processing.MessageId,
            cancellationToken);
        return new StoredUserMessage(message, processing);
    }

    public async Task<StoredUserMessage> AddOrGetByDeliveryKeyAsync(
        StoredUserMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await context.ChatMessages.AddAsync(message.Message, cancellationToken);
        await context.UserMessageProcessings.AddAsync(message.Processing, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return message;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            var existing = await GetByDeliveryKeyAsync(
                message.Message.UserId,
                message.Processing.DeliveryKey,
                cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
