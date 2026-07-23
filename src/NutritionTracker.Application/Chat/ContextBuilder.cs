using System.Globalization;
using System.Text;
using System.Text.Json;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Chat;

public sealed class ContextBuilder(ContextBuilderSettings settings) : IContextBuilder
{
    public ContextBuildResult Build(ContextBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSettings(settings);

        var instructions = BuildInstructions(request, includeSummary: false);
        var currentMessage = new LanguageModelInputMessage(ChatRole.User, request.CurrentUserMessage);
        var toolOutputSize = request.ToolOutputs.Sum(output => output.OutputJson.Length);
        var essentialSize = instructions.Length + currentMessage.Content.Length +
            toolOutputSize;
        if (essentialSize > settings.MaximumCharacters)
        {
            throw new InvalidOperationException(
                "The system instruction, current message, workflow state, settings, and tool outputs " +
                "exceed the configured context size.");
        }

        if (request.IncludeConversationMessages && request.Snapshot.Summary is not null)
        {
            var instructionsWithSummary = BuildInstructions(request, includeSummary: true);
            var sizeWithSummary = instructionsWithSummary.Length + currentMessage.Content.Length +
                toolOutputSize;
            if (sizeWithSummary <= settings.MaximumCharacters)
            {
                instructions = instructionsWithSummary;
                essentialSize = sizeWithSummary;
            }
        }

        var messages = new List<LanguageModelInputMessage>();
        var characterCount = essentialSize;
        if (request.IncludeConversationMessages)
        {
            foreach (var candidate in SelectCandidates(request))
            {
                if (characterCount + candidate.Content.Length > settings.MaximumCharacters)
                {
                    continue;
                }

                messages.Add(new LanguageModelInputMessage(
                    candidate.Role,
                    candidate.Content,
                    candidate.Sequence));
                characterCount += candidate.Content.Length;
            }

            messages.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
            messages.Add(currentMessage with { Sequence = long.MaxValue });
        }

        return new ContextBuildResult(
            instructions,
            messages,
            request.ToolOutputs.ToArray(),
            characterCount);
    }

    private static string BuildInstructions(ContextBuildRequest request, bool includeSummary)
    {
        var builder = new StringBuilder(request.SystemInstruction);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("Current trusted database context:");
        builder.Append("- User settings: ");
        builder.Append(JsonSerializer.Serialize(request.Snapshot.UserSettings));
        builder.AppendLine();
        builder.AppendLine(
            "- Database values and successful tool results override conversation history and summaries.");
        builder.AppendLine(
            "- Conversation history and summaries are non-authoritative clues only. Re-read current " +
            "database data with an allowlisted tool before stating factual nutrition, food, recipe, or diary data.");

        if (request.Snapshot.PendingState is { } pending)
        {
            builder.Append("- Unfinished clarification or confirmation: ");
            builder.Append(JsonSerializer.Serialize(new
            {
                pending.SourceMessageId,
                State = pending.State.ToString(),
                pending.OriginalMessage,
                pending.PendingQuestion,
                pending.ToolName,
                pending.ToolArgumentsJson
            }));
            builder.AppendLine();
        }

        if (includeSummary && request.Snapshot.Summary is { } summary)
        {
            builder.Append("- Non-authoritative conversation summary: ");
            builder.Append(JsonSerializer.Serialize(summary));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private Candidate[] SelectCandidates(ContextBuildRequest request)
    {
        var currentTerms = GetTerms(request.CurrentUserMessage);
        var candidates = request.Snapshot.RecentMessages
            .Where(message => message.SourceMessageId != request.CurrentMessageId)
            .Where(message => message.Role is ChatRole.User or ChatRole.Assistant)
            .Select(message => new Candidate(
                message.SourceMessageId,
                message.Role,
                message.Content,
                message.CreatedAtUtc.UtcTicks * 2 + (message.Role == ChatRole.Assistant ? 1 : 0),
                CountOverlap(currentTerms, GetTerms(message.Content))))
            .OrderByDescending(candidate => candidate.Sequence)
            .ToArray();

        var selected = candidates
            .Take(settings.MinimumRecentMessages)
            .Concat(candidates
                .Where(candidate => candidate.Relevance > 0)
                .OrderByDescending(candidate => candidate.Relevance)
                .ThenByDescending(candidate => candidate.Sequence))
            .DistinctBy(candidate => (candidate.SourceMessageId, candidate.Role))
            .Take(settings.MaximumRecentMessages)
            .ToArray();

        return selected;
    }

    private static HashSet<string> GetTerms(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKC);
        var separated = new string(normalized
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());
        return separated
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 3)
            .Select(term => term.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int CountOverlap(HashSet<string> currentTerms, HashSet<string> candidateTerms)
    {
        return currentTerms.Count == 0 ? 0 : currentTerms.Count(candidateTerms.Contains);
    }

    private static void ValidateSettings(ContextBuilderSettings value)
    {
        if (value.MaximumCharacters <= 0 || value.MaximumRecentMessages < 0 ||
            value.MinimumRecentMessages < 0 ||
            value.MinimumRecentMessages > value.MaximumRecentMessages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                string.Create(CultureInfo.InvariantCulture,
                    $"Invalid context settings: {JsonSerializer.Serialize(value)}"));
        }
    }

    private sealed record Candidate(
        Guid SourceMessageId,
        ChatRole Role,
        string Content,
        long Sequence,
        int Relevance);
}
