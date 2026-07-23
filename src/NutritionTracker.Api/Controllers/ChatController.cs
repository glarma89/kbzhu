using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Chat;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/chat/messages")]
public sealed class ChatController(IChatMessageService chatMessageService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ChatMessageResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatMessageResult>> SendAsync(
        ChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await chatMessageService.SendAsync(
            new SendChatMessageCommand(
                request.UserId,
                request.Message,
                request.ClientMessageId,
                request.OccurredAt),
            cancellationToken));
    }
}

public sealed record ChatMessageRequest(
    Guid UserId,
    string Message,
    string ClientMessageId,
    DateTimeOffset? OccurredAt);
