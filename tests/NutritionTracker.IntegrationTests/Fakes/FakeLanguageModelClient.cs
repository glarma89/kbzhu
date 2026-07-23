using NutritionTracker.Application.Chat;

namespace NutritionTracker.IntegrationTests.Fakes;

internal sealed class FakeLanguageModelClient(IEnumerable<LanguageModelResponse> responses)
    : ILanguageModelClient
{
    private readonly Queue<LanguageModelResponse> _responses = new(responses);
    private readonly List<LanguageModelRequest> _requests = [];

    public IReadOnlyList<LanguageModelRequest> Requests => _requests;

    public Exception? ExceptionWhenResponsesExhausted { get; init; }

    public Task<LanguageModelResponse> CreateResponseAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        if (_responses.Count == 0)
        {
            if (ExceptionWhenResponsesExhausted is not null)
            {
                throw ExceptionWhenResponsesExhausted;
            }

            throw new InvalidOperationException("No fake language model response was configured.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
