using System.Text.Json;
using System.Text.Json.Nodes;

namespace NutritionTracker.Application.Chat;

public sealed record LanguageModelToolDefinition(
    string Name,
    string Description,
    JsonElement ParametersJsonSchema,
    bool Strict);

public sealed record LanguageModelToolOutput(string CallId, string OutputJson);

public sealed record LanguageModelRequest(
    string Instructions,
    string? UserMessage,
    string? PreviousResponseId,
    IReadOnlyList<LanguageModelToolOutput> ToolOutputs,
    IReadOnlyList<LanguageModelToolDefinition> Tools);

public sealed record LanguageModelToolCall(string CallId, string Name, string ArgumentsJson);

public sealed record LanguageModelResponse(
    string ResponseId,
    string? OutputText,
    IReadOnlyList<LanguageModelToolCall> ToolCalls);

public interface ILanguageModelClient
{
    Task<LanguageModelResponse> CreateResponseAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken);
}

public static class StrictToolSchemaFactory
{
    public static JsonElement Create(JsonElement source)
    {
        var node = JsonNode.Parse(source.GetRawText())
            ?? throw new JsonException("A tool schema is required.");
        var normalized = Normalize(node);
        using var document = JsonDocument.Parse(normalized.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonNode Normalize(JsonNode node)
    {
        if (node is JsonArray array)
        {
            return new JsonArray(array.Select(item =>
                item is null ? null : Normalize(item)).ToArray());
        }

        if (node is not JsonObject source)
        {
            return node.DeepClone();
        }

        var result = new JsonObject();
        foreach (var property in source)
        {
            if (property.Key is "$schema" or "oneOf" or "not")
            {
                continue;
            }

            result[property.Key] = property.Value is null ? null : Normalize(property.Value);
        }

        if (source["properties"] is JsonObject sourceProperties)
        {
            var originallyRequired = source["required"] is JsonArray required
                ? required.Select(item => item?.GetValue<string>())
                    .Where(item => item is not null)
                    .ToHashSet(StringComparer.Ordinal)
                : [];
            var strictProperties = new JsonObject();
            foreach (var property in sourceProperties)
            {
                var normalizedProperty = property.Value is null
                    ? new JsonObject()
                    : Normalize(property.Value);
                strictProperties[property.Key] = originallyRequired.Contains(property.Key)
                    ? normalizedProperty
                    : MakeNullable(normalizedProperty);
            }

            result["properties"] = strictProperties;
            result["required"] = new JsonArray(
                sourceProperties.Select(property => JsonValue.Create(property.Key)).ToArray());
            result["additionalProperties"] = false;
        }

        return result;
    }

    private static JsonObject MakeNullable(JsonNode schema)
    {
        if (schema is JsonObject schemaObject && schemaObject["type"] is JsonValue typeValue)
        {
            schemaObject["type"] = new JsonArray(typeValue.DeepClone(), JsonValue.Create("null"));
            return schemaObject;
        }

        return new JsonObject
        {
            ["anyOf"] = new JsonArray(
                schema,
                new JsonObject { ["type"] = "null" })
        };
    }
}

public sealed class LanguageModelUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
