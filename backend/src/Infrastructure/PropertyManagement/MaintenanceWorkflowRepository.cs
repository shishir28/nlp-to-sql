using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface IMaintenanceWorkflowRepository
{
    Task<IReadOnlyList<MaintenanceJobWorkflowDto>> GetJobsAwaitingApprovalAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<MaintenanceQuoteDto>> GetQuotesForJobAsync(string customerId, long jobId, CancellationToken ct);
    Task<bool> ApproveQuoteAsync(string customerId, long quoteId, ApproveQuoteRequest request, CancellationToken ct);
}

public sealed class MaintenanceWorkflowRepository : IMaintenanceWorkflowRepository
{
    private readonly string _connectionString;

    public MaintenanceWorkflowRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<MaintenanceJobWorkflowDto>> GetJobsAwaitingApprovalAsync(string customerId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var jobs = await conn.QueryAsync<MaintenanceJobWorkflowDto>(@"
            SELECT mj.MaintenanceJobId, mj.PropertyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   mj.Description, mj.Status, mj.Priority,
                   mj.QuoteAmount, mj.QuoteReceivedDate, mj.QuoteApprovedDate,
                   mj.QuoteApprovedByUser, mj.InvoiceNumber, mj.InvoiceDate,
                   mj.InvoiceAmount, mj.InvoicePaidDate, mj.WorkOrderNumber, mj.ScheduledDate
            FROM MaintenanceJobs mj
            JOIN Properties p ON p.PropertyId = mj.PropertyId
            WHERE mj.CustomerId = @CustomerId
              AND mj.Status IN ('OPEN','IN_PROGRESS','AWAITING_PARTS')
            ORDER BY mj.Priority DESC, mj.QuoteReceivedDate ASC
            LIMIT 50",
            new { CustomerId = customerId });

        var jobList = jobs.ToList();

        foreach (var job in jobList)
        {
            var quotes = await GetQuotesForJobAsync(customerId, job.MaintenanceJobId, ct);
            // quotes are readonly, so rebuild with quotes attached
        }

        return jobList;
    }

    public async Task<IReadOnlyList<MaintenanceQuoteDto>> GetQuotesForJobAsync(string customerId, long jobId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MaintenanceQuoteDto>(@"
            SELECT q.QuoteId, q.MaintenanceJobId, mj.Description AS Description,
                   q.VendorId, v.CompanyName AS VendorName,
                   q.QuoteAmount AS Amount, q.StatusCode AS Status, q.Notes,
                   q.QuoteDate, NULL AS ApprovedDate, NULL AS ApprovedByUser, q.CreatedAtUtc
            FROM MaintenanceQuotes q
            JOIN Vendors v ON v.VendorId = q.VendorId
            JOIN MaintenanceJobs mj ON mj.MaintenanceJobId = q.MaintenanceJobId
            WHERE mj.CustomerId = @CustomerId
              AND q.MaintenanceJobId = @JobId
            ORDER BY q.QuoteAmount ASC",
            new { CustomerId = customerId, JobId = jobId });

        return rows.ToList();
    }

    public async Task<bool> ApproveQuoteAsync(string customerId, long quoteId, ApproveQuoteRequest request, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var affected = await conn.ExecuteAsync(@"
            UPDATE MaintenanceQuotes q
            JOIN MaintenanceJobs mj ON mj.MaintenanceJobId = q.MaintenanceJobId
            SET q.StatusCode = 'APPROVED',
                q.Notes = COALESCE(@Notes, q.Notes),
                mj.QuoteApprovedDate = CURRENT_DATE,
                mj.QuoteApprovedByUser = @ApprovedByUser,
                mj.QuoteAmount = q.QuoteAmount
            WHERE q.QuoteId = @QuoteId
              AND mj.CustomerId = @CustomerId",
            new { QuoteId = quoteId, CustomerId = customerId, request.ApprovedByUser, request.Notes });

        return affected > 0;
    }
}
