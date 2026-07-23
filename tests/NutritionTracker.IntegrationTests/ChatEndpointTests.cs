using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NutritionTracker.Api.Controllers;
using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;
using NutritionTracker.Infrastructure.Persistence;
using NutritionTracker.IntegrationTests.Fakes;

namespace NutritionTracker.IntegrationTests;

public sealed class ChatEndpointTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuildsLimitedContextFromPersistedConversationAndCurrentUserSettings()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "Asia/Jerusalem", UtcNow);
        var target = new NutritionTarget(
            Guid.NewGuid(),
            user.Id,
            new DateOnly(2026, 7, 1),
            new NutritionValues(2200m, 150m, 75m, 230m));
        var fake = new FakeLanguageModelClient([
            new LanguageModelResponse("response-history", "Яблоко обсуждено.", []),
            new LanguageModelResponse("response-current", "Продолжаем.", [])
        ]);
        using var factory = CreateFactory(fake);
        await factory.SeedAsync(user, target);
        using (var scope = factory.Services.CreateScope())
        {
            var source = scope.ServiceProvider.GetRequiredService<IChatContextSource>();
            _ = await source.GetAsync(
                new ChatContextSourceRequest(user.Id, Guid.NewGuid(), UtcNow),
                CancellationToken.None);
        }
        using var client = factory.CreateAuthenticatedClient(user.Id);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new ChatMessageRequest("Запомни разговор про яблоко", "context-history", UtcNow),
            CancellationToken.None);
        using var secondResponse = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new ChatMessageRequest("Продолжим про яблоко", "context-current", UtcNow),
            CancellationToken.None);

        Assert.True(
            firstResponse.StatusCode == HttpStatusCode.OK,
            await firstResponse.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.True(
            secondResponse.StatusCode == HttpStatusCode.OK,
            await secondResponse.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(2, fake.Requests.Count);
        var contextRequest = fake.Requests[1];
        Assert.Collection(
            contextRequest.Messages,
            message =>
            {
                Assert.Equal(NutritionTracker.Domain.Chat.ChatRole.User, message.Role);
                Assert.Equal("Запомни разговор про яблоко", message.Content);
            },
            message =>
            {
                Assert.Equal(NutritionTracker.Domain.Chat.ChatRole.Assistant, message.Role);
                Assert.Equal("Яблоко обсуждено.", message.Content);
            },
            message => Assert.Equal("Продолжим про яблоко", message.Content));
        Assert.Contains("Asia/Jerusalem", contextRequest.Instructions, StringComparison.Ordinal);
        Assert.Contains("2200", contextRequest.Instructions, StringComparison.Ordinal);
        Assert.Contains("grams", contextRequest.Instructions, StringComparison.Ordinal);
        Assert.Contains("override conversation history", contextRequest.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedPendingUpdateExecutesOnceAndRepeatedDeliveryReplays()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "UTC", UtcNow);
        var food = new FoodProduct(
            Guid.NewGuid(), user.Id, "Apple", null,
            new NutritionValues(100m, 1m, 0m, 25m), null, "Test", false, UtcNow, UtcNow);
        var arguments = ToolJson.Serialize(new UpdateFoodArguments(
            food.Id, "Green apple", null, 105m, 1m, 0m, 26m, null, "Correct label"));
        var fake = new FakeLanguageModelClient([
            new LanguageModelResponse("response-confirm", null,
                [new LanguageModelToolCall("call-update", "update_food", arguments)])
        ]);
        using var factory = CreateFactory(fake);
        await factory.SeedAsync(user, food);
        using var client = factory.CreateAuthenticatedClient(user.Id);

        using var pendingResponse = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new ChatMessageRequest("Update the apple", "confirm-message", UtcNow),
            CancellationToken.None);
        var pending = await pendingResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);
        Assert.NotNull(pending);
        Assert.NotNull(pending.PendingConfirmation);

        using var confirmedResponse = await client.PostAsJsonAsync(
            $"/api/chat/messages/{pending.MessageId}/confirmation",
            new ChatConfirmationRequest(true),
            CancellationToken.None);
        using var repeatedResponse = await client.PostAsJsonAsync(
            $"/api/chat/messages/{pending.MessageId}/confirmation",
            new ChatConfirmationRequest(true),
            CancellationToken.None);
        var confirmed = await confirmedResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);
        Assert.Equal(confirmed?.MessageId, repeated?.MessageId);
        Assert.Equal(confirmed?.AssistantMessage, repeated?.AssistantMessage);
        Assert.Equal(confirmed?.ExecutedActions, repeated?.ExecutedActions);
        Assert.Single(confirmed!.ExecutedActions);
        Assert.Single(fake.Requests);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        Assert.Equal("Green apple", (await context.FoodProducts.SingleAsync(
            item => item.Id == food.Id, CancellationToken.None)).Name);
        Assert.Equal(1, await context.ToolExecutions.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CancelledPendingUpdateIsTerminalAndNeverExecutes()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "UTC", UtcNow);
        var food = new FoodProduct(
            Guid.NewGuid(), user.Id, "Apple", null,
            new NutritionValues(100m, 1m, 0m, 25m), null, "Test", false, UtcNow, UtcNow);
        var arguments = ToolJson.Serialize(new UpdateFoodArguments(
            food.Id, "Changed", null, 105m, 1m, 0m, 26m, null, "Correct label"));
        var fake = new FakeLanguageModelClient([
            new LanguageModelResponse("response-cancel", null,
                [new LanguageModelToolCall("call-update", "update_food", arguments)])
        ]);
        using var factory = CreateFactory(fake);
        await factory.SeedAsync(user, food);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var pendingResponse = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new ChatMessageRequest("Update the apple", "cancel-message", UtcNow),
            CancellationToken.None);
        var pending = await pendingResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);

        using var cancelledResponse = await client.PostAsJsonAsync(
            $"/api/chat/messages/{pending!.MessageId}/confirmation",
            new ChatConfirmationRequest(false),
            CancellationToken.None);
        using var repeatedResponse = await client.PostAsJsonAsync(
            $"/api/chat/messages/{pending.MessageId}/confirmation",
            new ChatConfirmationRequest(false),
            CancellationToken.None);
        var cancelled = await cancelledResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);

        Assert.Equal(cancelled?.MessageId, repeated?.MessageId);
        Assert.Equal(cancelled?.AssistantMessage, repeated?.AssistantMessage);
        Assert.Empty(cancelled!.ExecutedActions);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        Assert.Equal("Apple", (await context.FoodProducts.SingleAsync(
            item => item.Id == food.Id, CancellationToken.None)).Name);
        Assert.Equal(0, await context.ToolExecutions.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ExecutesSequentialToolsAndReturnsBackendDailySummary()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "UTC", UtcNow);
        var food = new FoodProduct(
            Guid.NewGuid(),
            null,
            "Apple",
            null,
            new NutritionValues(200m, 20m, 10m, 30m),
            null,
            "Test",
            true,
            UtcNow,
            UtcNow);
        var fake = new FakeLanguageModelClient(
        [
            new LanguageModelResponse(
                "response-1",
                null,
                [new LanguageModelToolCall(
                    "call-search",
                    "search_foods",
                    "{\"query\":\"apple\",\"limit\":10}")]),
            new LanguageModelResponse(
                "response-2",
                null,
                [new LanguageModelToolCall(
                    "call-add",
                    "add_food_to_diary",
                    "{\"food_product_id\":\"" + food.Id +
                    "\",\"weight_grams\":100,\"occurred_at\":\"2026-07-23T12:00:00Z\"," +
                    "\"meal_type\":\"lunch\",\"user_intent\":\"log the apple\"}")]),
            new LanguageModelResponse("response-3", "Всего 9999 ккал.", [])
        ]);
        using var factory = CreateFactory(fake);
        await factory.SeedAsync(user, food);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var request = new ChatMessageRequest(
            "Добавь 100 г яблока на обед",
            "client-message-1",
            UtcNow);

        using var response = await client.PostAsJsonAsync(
            "/api/chat/messages", request, CancellationToken.None);
        var result = await response.Content.ReadFromJsonAsync<ChatMessageResult>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Collection(
            result.ExecutedActions,
            action => Assert.Equal("search_foods", action.ToolName),
            action => Assert.Equal("add_food_to_diary", action.ToolName));
        Assert.Equal(200m, result.DailySummary?.Consumed.Calories);
        Assert.Contains("200", result.AssistantMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("9999", result.AssistantMessage, StringComparison.Ordinal);
        Assert.Null(result.PendingClarification);
        Assert.Null(result.PendingConfirmation);

        Assert.Equal(3, fake.Requests.Count);
        Assert.All(fake.Requests[0].Tools, tool => Assert.True(tool.Strict));
        Assert.Equal(15, fake.Requests[0].Tools.Count);
        Assert.All(fake.Requests[0].Tools, AssertStrictSchema);
        Assert.Equal(request.Message, fake.Requests[0].UserMessage);
        Assert.Null(fake.Requests[1].UserMessage);
        Assert.Equal("response-1", fake.Requests[1].PreviousResponseId);
        Assert.Single(fake.Requests[1].ToolOutputs);
        Assert.Equal("response-2", fake.Requests[2].PreviousResponseId);

        using var repeatedResponse = await client.PostAsJsonAsync(
            "/api/chat/messages", request, CancellationToken.None);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<ChatMessageResult>(
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(result.AssistantMessage, repeated?.AssistantMessage);
        Assert.Equal(result.DailySummary?.Consumed, repeated?.DailySummary?.Consumed);
        Assert.Equal(
            result.ExecutedActions.Select(action => action.ToolName),
            repeated?.ExecutedActions.Select(action => action.ToolName));
        Assert.Equal(3, fake.Requests.Count);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        Assert.Equal(1, await context.ChatMessages.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.MealItems.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.ToolExecutions.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.ProcessedCommands.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StopsAtConfiguredAgentIterationLimitWithoutCallingOpenAi()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "UTC", UtcNow);
        var toolCall = new LanguageModelToolCall(
            "call-summary",
            "get_daily_summary",
            "{\"date\":\"2026-07-23\"}");
        var fake = new FakeLanguageModelClient(
        [
            new LanguageModelResponse("response-1", null, [toolCall]),
            new LanguageModelResponse("response-2", null, [toolCall with { CallId = "call-summary-2" }])
        ]);
        using var factory = CreateFactory(fake, maximumIterations: 2);
        await factory.SeedAsync(user);
        using var client = factory.CreateAuthenticatedClient(user.Id);

        using var response = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new ChatMessageRequest("Покажи итог", "client-message-limit", UtcNow),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task DoesNotRepeatMutationAfterAmbiguousLanguageModelFailure()
    {
        var user = new UserProfile(Guid.NewGuid(), "Chat user", "UTC", UtcNow);
        var food = new FoodProduct(
            Guid.NewGuid(),
            null,
            "Apple",
            null,
            new NutritionValues(100m, 1m, 0m, 25m),
            null,
            "Test",
            true,
            UtcNow,
            UtcNow);
        var fake = new FakeLanguageModelClient(
        [
            new LanguageModelResponse(
                "response-1",
                null,
                [new LanguageModelToolCall(
                    "call-add",
                    "add_food_to_diary",
                    "{\"food_product_id\":\"" + food.Id +
                    "\",\"weight_grams\":100,\"occurred_at\":\"2026-07-23T12:00:00Z\"," +
                    "\"meal_type\":\"lunch\",\"user_intent\":\"log the apple\"}")])
        ])
        {
            ExceptionWhenResponsesExhausted = new LanguageModelUnavailableException(
                "Simulated ambiguous network failure.")
        };
        using var factory = CreateFactory(fake);
        await factory.SeedAsync(user, food);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var request = new ChatMessageRequest(
            "Добавь яблоко", "client-message-ambiguous", UtcNow);

        using var failedResponse = await client.PostAsJsonAsync(
            "/api/chat/messages", request, CancellationToken.None);
        using var repeatedResponse = await client.PostAsJsonAsync(
            "/api/chat/messages", request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        Assert.Equal(1, await context.MealItems.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.ToolExecutions.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.ProcessedCommands.CountAsync(CancellationToken.None));
    }

    private static FoodApiWebApplicationFactory CreateFactory(
        FakeLanguageModelClient fake,
        int maximumIterations = 8)
    {
        return new FoodApiWebApplicationFactory(services =>
        {
            services.RemoveAll<ILanguageModelClient>();
            services.AddSingleton<ILanguageModelClient>(fake);
            services.RemoveAll<ChatAgentSettings>();
            services.AddSingleton(new ChatAgentSettings(
                maximumIterations, TimeSpan.FromSeconds(30)));
        });
    }

    private static void AssertStrictSchema(LanguageModelToolDefinition tool)
    {
        var schema = tool.ParametersJsonSchema;
        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.False(schema.TryGetProperty("$schema", out _));
        Assert.False(schema.TryGetProperty("oneOf", out _));
        Assert.False(schema.TryGetProperty("not", out _));
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        var propertyNames = schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal);
        var requiredNames = schema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal);
        Assert.Equal(propertyNames, requiredNames);
    }
}
