using Application.Abstractions;
using Application.Orchestration;
using Infrastructure.Agents;
using Infrastructure.Data;
using Infrastructure.Logging;
using Infrastructure.Policy;
using Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HTTP clients
builder.Services.AddHttpClient();

// Register application services
builder.Services.AddScoped<INlSqlOrchestrator, NlSqlOrchestrator>();
builder.Services.AddScoped<INl2SqlAgentClient, Nl2SqlAgentClient>();
builder.Services.AddScoped<ISummarizationClient, SummarizationClient>();
builder.Services.AddSingleton<ISchemaPolicyProvider, JsonSchemaPolicyProvider>();
builder.Services.AddScoped<ISqlFirewall, SqlFirewall>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();
builder.Services.AddScoped<IAuditLogger, ConsoleAuditLogger>();

// CORS for local dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("LocalDev");
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("🚀 API Server starting on http://0.0.0.0:5000");
app.Run();
