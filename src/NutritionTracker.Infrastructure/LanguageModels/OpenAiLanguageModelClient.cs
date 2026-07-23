using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NutritionTracker.Application.Chat;

namespace NutritionTracker.Infrastructure.LanguageModels;

internal sealed class OpenAiLanguageModelClient(OpenAiLanguageModelOptions options)
    : ILanguageModelClient, IDisposable
{
    private static readonly Uri ResponsesEndpoint = new("https://api.openai.com/v1/responses");
    private readonly HttpClient _httpClient = CreateHttpClient(options.ApiKey);

    public async Task<LanguageModelResponse> CreateResponseAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureApiKeyConfigured();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.RequestTimeout);
        var requestJson = BuildRequestJson(request);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };
                using var response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(
                        timeoutSource.Token);
                    using var document = await JsonDocument.ParseAsync(
                        stream,
                        cancellationToken: timeoutSource.Token);
                    return Map(document.RootElement);
                }

                if (IsTransient(response.StatusCode) && attempt < options.MaximumRetries)
                {
                    await DelayAsync(attempt, response.Headers.RetryAfter?.Delta, timeoutSource.Token);
                    continue;
                }

                throw new LanguageModelUnavailableException(
                    IsTransient(response.StatusCode)
                        ? "OpenAI is temporarily unavailable."
                        : $"OpenAI rejected the request with HTTP {(int)response.StatusCode}.");
            }
            catch (HttpRequestException) when (attempt < options.MaximumRetries)
            {
                await DelayAsync(attempt, null, timeoutSource.Token);
            }
            catch (IOException) when (attempt < options.MaximumRetries)
            {
                await DelayAsync(attempt, null, timeoutSource.Token);
            }
            catch (HttpRequestException exception)
            {
                throw new LanguageModelUnavailableException("OpenAI could not be reached.", exception);
            }
            catch (IOException exception)
            {
                throw new LanguageModelUnavailableException(
                    "The OpenAI response could not be read.", exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new LanguageModelUnavailableException("The OpenAI request timed out.", exception);
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private string BuildRequestJson(LanguageModelRequest request)
    {
        var input = new JsonArray();
        if (request.UserMessage is not null)
        {
            input.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = request.UserMessage
            });
        }

        foreach (var output in request.ToolOutputs)
        {
            input.Add(new JsonObject
            {
                ["type"] = "function_call_output",
                ["call_id"] = output.CallId,
                ["output"] = output.OutputJson
            });
        }

        var tools = new JsonArray();
        foreach (var tool in request.Tools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema.GetRawText()),
                ["strict"] = tool.Strict
            });
        }

        var payload = new JsonObject
        {
            ["model"] = options.Model,
            ["instructions"] = request.Instructions,
            ["input"] = input,
            ["tools"] = tools,
            ["parallel_tool_calls"] = true,
            ["store"] = true
        };
        if (request.PreviousResponseId is not null)
        {
            payload["previous_response_id"] = request.PreviousResponseId;
        }

        return payload.ToJsonString();
    }

    private static LanguageModelResponse Map(JsonElement response)
    {
        var responseId = response.GetProperty("id").GetString()
            ?? throw new JsonException("The OpenAI response has no id.");
        var calls = new List<LanguageModelToolCall>();
        var text = new List<string>();
        foreach (var item in response.GetProperty("output").EnumerateArray())
        {
            var type = item.GetProperty("type").GetString();
            if (string.Equals(type, "function_call", StringComparison.Ordinal))
            {
                calls.Add(new LanguageModelToolCall(
                    item.GetProperty("call_id").GetString()
                        ?? throw new JsonException("A function call has no call_id."),
                    item.GetProperty("name").GetString()
                        ?? throw new JsonException("A function call has no name."),
                    item.GetProperty("arguments").GetString()
                        ?? throw new JsonException("A function call has no arguments.")));
            }
            else if (string.Equals(type, "message", StringComparison.Ordinal) &&
                item.TryGetProperty("content", out var content))
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var partType) &&
                        string.Equals(partType.GetString(), "output_text", StringComparison.Ordinal) &&
                        part.TryGetProperty("text", out var partText) &&
                        !string.IsNullOrWhiteSpace(partText.GetString()))
                    {
                        text.Add(partText.GetString()!);
                    }
                }
            }
        }

        return new LanguageModelResponse(
            responseId,
            text.Count == 0 ? null : string.Join(Environment.NewLine, text),
            calls);
    }

    private async Task DelayAsync(
        int attempt,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        var multiplier = Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * 0.25 + 0.875;
        var exponentialDelay = TimeSpan.FromMilliseconds(
            options.InitialRetryDelay.TotalMilliseconds * multiplier * jitter);
        var delay = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
            ? retryAfter.Value
            : exponentialDelay;
        await Task.Delay(delay, cancellationToken);
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new LanguageModelUnavailableException(
                "OpenAI API key is not configured. Set OPENAI_API_KEY or the OpenAI:ApiKey user secret.");
        }
    }

    private static HttpClient CreateHttpClient(string? apiKey)
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return client;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;
}
