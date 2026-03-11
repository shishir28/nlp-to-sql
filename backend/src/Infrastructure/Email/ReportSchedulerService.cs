using Application.Abstractions;
using Contracts.Requests;
using Infrastructure.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Infrastructure.Email;

/// <summary>
/// Background service that checks for due scheduled reports every 5 minutes,
/// executes the query, and emails results to the recipient.
/// SMTP is optional — set Smtp:Host in config to enable sending.
/// </summary>
public sealed class ReportSchedulerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportSchedulerService> _logger;

    public ReportSchedulerService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<ReportSchedulerService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueReportsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled reports");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessDueReportsAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduledReportRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<INlSqlOrchestrator>();

        var dueReports = await repo.GetDueReportsAsync(ct);
        if (dueReports.Count == 0) return;

        _logger.LogInformation("Processing {Count} due scheduled reports", dueReports.Count);

        foreach (var report in dueReports)
        {
            try
            {
                var request = new NlQueryRequest { Question = report.Question };
                var result = await orchestrator.HandleAsync(
                    request,
                    customerId: "1",  // scheduled reports run under the owning customer
                    userId: "scheduler",
                    role: report.Role,
                    cancellationToken: ct);

                if (result.Status == "ok")
                {
                    await SendReportEmailAsync(report.RecipientEmail, report.Name, report.Question, result, ct);
                }

                var nextRun = ScheduledReportRepository.ComputeNextRun(report.Schedule, DateTime.UtcNow);
                await repo.UpdateLastRunAsync(report.Id, nextRun, ct);

                _logger.LogInformation("Ran scheduled report {Name} → {RowCount} rows", report.Name, result.RowCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run scheduled report {Id}: {Name}", report.Id, report.Name);
            }
        }
    }

    private async Task SendReportEmailAsync(
        string recipient, string reportName, string question,
        Contracts.Responses.QueryResponse result, CancellationToken ct)
    {
        var smtpHost = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("SMTP not configured — skipping email for report '{Name}'", reportName);
            return;
        }

        var port = _configuration.GetValue<int>("Smtp:Port", 587);
        var user = _configuration["Smtp:Username"] ?? "";
        var pass = _configuration["Smtp:Password"] ?? "";
        var from = _configuration["Smtp:From"] ?? "reports@property-analytics.local";

        var html = BuildEmailHtml(reportName, question, result);

        using var client = new SmtpClient(smtpHost, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };

        var mail = new MailMessage(from, recipient)
        {
            Subject = $"[Property Analytics] {reportName} — {DateTime.UtcNow:yyyy-MM-dd}",
            Body = html,
            IsBodyHtml = true
        };

        await client.SendMailAsync(mail, ct);
        _logger.LogInformation("Sent report email '{Name}' to {Recipient}", reportName, recipient);
    }

    private static string BuildEmailHtml(string name, string question, Contracts.Responses.QueryResponse result)
    {
        var cols = result.Columns.Count > 0
            ? result.Columns.Select(c => c.Name).ToList()
            : (result.Rows.Count > 0 ? result.Rows[0].Keys.ToList() : new List<string>());

        var headers = string.Join("", cols.Select(c =>
            $"<th style='padding:6px 10px;background:#1e7dc8;color:#fff;text-align:left;font-size:12px'>{c}</th>"));

        var rows = string.Join("", result.Rows.Take(50).Select(row =>
        {
            var cells = string.Join("", cols.Select(c =>
                $"<td style='padding:5px 10px;border-bottom:1px solid #e5e7eb;font-size:12px'>{row.GetValueOrDefault(c)}</td>"));
            return $"<tr>{cells}</tr>";
        }));

        return $@"
<html><body style='font-family:sans-serif;color:#111'>
<h2 style='color:#1e7dc8'>{name}</h2>
<p style='color:#6b7280'>Query: <em>{question}</em></p>
<p><strong>{result.RowCount} rows</strong> returned</p>
{(result.NlSummary != null ? $"<p style='background:#f0fdf4;padding:10px;border-radius:6px'>{result.NlSummary}</p>" : "")}
<table style='border-collapse:collapse;width:100%'>
  <thead><tr>{headers}</tr></thead>
  <tbody>{rows}</tbody>
</table>
{(result.RowCount > 50 ? $"<p style='color:#9ca3af;font-size:11px'>Showing first 50 of {result.RowCount} rows</p>" : "")}
<hr/><p style='color:#9ca3af;font-size:11px'>Property Analytics — automated report</p>
</body></html>";
    }
}
