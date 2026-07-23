using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NutritionTracker.Application.Tools;

public enum ToolConfirmationRequirement
{
    None,
    Required
}

public enum ToolIdempotencyRequirement
{
    NotApplicable,
    Required
}

public sealed record ToolConfirmationEvidence(
    string ToolName,
    string CanonicalArgumentsHash,
    DateTimeOffset ConfirmedAtUtc);

public sealed record ToolInvocationContext(
    Guid UserId,
    string? IdempotencyKey,
    Guid? SourceMessageId,
    ToolConfirmationEvidence? Confirmation);

public sealed record ToolErrorDefinition(string Code, string Description, bool IsRetryable);

public sealed record ToolError(string Code, string Message, string? Field = null);

public sealed record ToolExecutionResult<TResult>(TResult? Result, ToolError? Error)
{
    public bool IsSuccess => Error is null;
}

public static class ToolExecutionResults
{
    public static ToolExecutionResult<TResult> Success<TResult>(TResult result) => new(result, null);

    public static ToolExecutionResult<TResult> Failure<TResult>(
        string code,
        string message,
        string? field = null) =>
        new(default, new ToolError(code, message, field));
}

public sealed record ToolIdempotencyPolicy(
    ToolIdempotencyRequirement Requirement,
    string Description);

public sealed record ToolDefinition(
    string Name,
    string Purpose,
    Type ArgumentsType,
    JsonElement ArgumentsJsonSchema,
    IReadOnlyList<string> RequiredArguments,
    Type ResultType,
    string ResultDescription,
    IReadOnlyList<ToolErrorDefinition> Errors,
    ToolConfirmationRequirement ConfirmationRequirement,
    string ConfirmationDescription,
    ToolIdempotencyPolicy Idempotency);

public sealed record ToolValidationError(string Field, string Code, string Message);

public interface IToolArguments
{
    IReadOnlyList<ToolValidationError> Validate();
}

public sealed class ToolArgumentsValidationException : Exception
{
    public ToolArgumentsValidationException(IReadOnlyList<ToolValidationError> errors)
        : base("Tool arguments failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyList<ToolValidationError> Errors { get; }
}

public static class ToolJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    public static string Serialize(object value, Type type)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(type);
        return JsonSerializer.Serialize(value, type, SerializerOptions);
    }

    public static T Deserialize<T>(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new JsonException("The JSON value cannot be null.");
    }

    public static T DeserializeArguments<T>(string json) where T : IToolArguments
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var arguments = JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new JsonException("Tool arguments must be a JSON object.");
        Validate(arguments);
        return arguments;
    }

    public static IToolArguments DeserializeArguments(string json, Type argumentsType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(argumentsType);
        if (!typeof(IToolArguments).IsAssignableFrom(argumentsType))
        {
            throw new ArgumentException("The type must implement IToolArguments.", nameof(argumentsType));
        }

        var arguments = JsonSerializer.Deserialize(json, argumentsType, SerializerOptions) as IToolArguments
            ?? throw new JsonException("Tool arguments must be a JSON object.");
        Validate(arguments);
        return arguments;
    }

    private static void Validate(IToolArguments arguments)
    {
        var errors = arguments.Validate();
        if (errors.Count > 0)
        {
            throw new ToolArgumentsValidationException(errors);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false));
        options.MakeReadOnly();
        return options;
    }
}

public static class ToolArgumentsHash
{
    public static string Create(string toolName, string argumentsJson)
    {
        var definition = ToolCatalog.GetRequired(toolName);
        var arguments = ToolJson.DeserializeArguments(argumentsJson, definition.ArgumentsType);
        var canonicalJson = ToolJson.Serialize(arguments, definition.ArgumentsType);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }
}
