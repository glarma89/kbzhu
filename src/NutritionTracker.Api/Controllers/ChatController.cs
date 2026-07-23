using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Chat;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = "AuthenticatedUser")]
[Route("api/chat/messages")]
public sealed class ChatController(IChatMessageService chatMessageService, ICurrentUser currentUser)
    : ControllerBase
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
                currentUser.UserId,
                request.Message,
                request.ClientMessageId,
                request.OccurredAt),
            cancellationToken));
    }

    [HttpPost("{messageId:guid}/confirmation")]
    [ProducesResponseType<ChatMessageResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChatMessageResult>> ContinueConfirmationAsync(
        Guid messageId,
        ChatConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await chatMessageService.ContinueConfirmationAsync(
            new ContinueChatConfirmationCommand(messageId, currentUser.UserId, request.Confirm),
            cancellationToken));
    }
}

public sealed record ChatMessageRequest(
    string Message,
    string ClientMessageId,
    DateTimeOffset? OccurredAt);

public sealed record ChatConfirmationRequest(bool Confirm);
