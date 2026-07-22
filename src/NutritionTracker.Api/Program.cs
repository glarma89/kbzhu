using NutritionTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var nutritionDatabaseConnectionString = builder.Configuration
    .GetConnectionString("NutritionDatabase")
    ?? throw new InvalidOperationException("Connection string 'NutritionDatabase' is not configured.");

builder.Services.AddControllers();
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
