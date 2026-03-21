using Application.Abstractions;
using Application.Orchestration;
using Infrastructure.Agents;
using Infrastructure.Dashboard;
using Infrastructure.Data;
using Infrastructure.Email;
using Infrastructure.Logging;
using Infrastructure.Policy;
using Infrastructure.PropertyManagement;
using Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HTTP clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Nl2SqlAgent", client =>
{
    var baseUrl = builder.Configuration["AgentService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // covers LLM generation + network
});

// Register application services
builder.Services.AddScoped<INlSqlOrchestrator, NlSqlOrchestrator>();
builder.Services.AddScoped<INl2SqlAgentClient, Nl2SqlAgentClient>();
builder.Services.AddScoped<ISummarizationClient, SummarizationClient>();
builder.Services.AddSingleton<ISchemaPolicyProvider, JsonSchemaPolicyProvider>();
builder.Services.AddScoped<ISqlFirewall, SqlFirewall>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();
builder.Services.AddScoped<IAuditLogger, MySqlAuditLogger>();

// Dashboard / analytics / scheduled reports / widget layout
builder.Services.AddScoped<ISavedQueryRepository, SavedQueryRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IScheduledReportRepository, ScheduledReportRepository>();
builder.Services.AddScoped<IDashboardWidgetRepository, DashboardWidgetRepository>();
builder.Services.AddHostedService<ReportSchedulerService>();

// Property management domain repositories
builder.Services.AddScoped<IVacancyRepository, VacancyRepository>();
builder.Services.AddScoped<IMaintenanceWorkflowRepository, MaintenanceWorkflowRepository>();
builder.Services.AddScoped<IArrearsRepository, ArrearsRepository>();
builder.Services.AddScoped<ITrustLedgerRepository, TrustLedgerRepository>();
builder.Services.AddScoped<IComplianceRepository, ComplianceRepository>();
builder.Services.AddScoped<IPMTaskRepository, PMTaskRepository>();
builder.Services.AddScoped<ILeaseRenewalRepository, LeaseRenewalRepository>();

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
