using NutritionTracker.Api.Errors;
using NutritionTracker.Infrastructure;
using NutritionTracker.Application.Chat;
using NutritionTracker.Infrastructure.LanguageModels;

var builder = WebApplication.CreateBuilder(args);

var nutritionDatabaseConnectionString = builder.Configuration
    .GetConnectionString("NutritionDatabase")
    ?? throw new InvalidOperationException("Connection string 'NutritionDatabase' is not configured.");

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(nutritionDatabaseConnectionString);

var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OpenAI:ApiKey"];
var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-5.6-sol";
var openAiTimeoutSeconds = builder.Configuration.GetValue("OpenAI:TimeoutSeconds", 30);
var openAiMaximumRetries = builder.Configuration.GetValue("OpenAI:MaximumRetries", 3);
var agentMaximumIterations = builder.Configuration.GetValue("OpenAI:MaximumAgentIterations", 8);
var agentTimeoutSeconds = builder.Configuration.GetValue("OpenAI:AgentTimeoutSeconds", 90);
builder.Services.AddOpenAiLanguageModel(
    new OpenAiLanguageModelOptions(
        openAiApiKey,
        openAiModel,
        TimeSpan.FromSeconds(openAiTimeoutSeconds),
        openAiMaximumRetries,
        TimeSpan.FromMilliseconds(250)),
    new ChatAgentSettings(
        agentMaximumIterations,
        TimeSpan.FromSeconds(agentTimeoutSeconds)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
