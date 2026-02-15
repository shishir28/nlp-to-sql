using System.Data;
using System.Diagnostics;
using Application.Abstractions;
using Contracts.Responses;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Data;

/// <summary>
/// Executes SQL queries against MySQL with parameterized queries and timeout enforcement
/// </summary>
public sealed class QueryExecutor : IQueryExecutor
{
    private readonly string _connectionString;

    public QueryExecutor(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<(IReadOnlyList<Dictionary<string, object?>> Rows, IReadOnlyList<ColumnInfo> Columns, long ExecutionMs)>
        ExecuteAsync(string sql, object parameters, int timeoutMs, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var commandDefinition = new CommandDefinition(
                commandText: sql,
                parameters: parameters,
                commandTimeout: timeoutMs / 1000, // Dapper expects seconds
                commandType: CommandType.Text,
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync(commandDefinition);
            var rows = new List<Dictionary<string, object?>>();
            var columns = new List<ColumnInfo>();

            var firstRow = true;
            foreach (var row in results)
            {
                var dict = (IDictionary<string, object?>)row;
                
                if (firstRow)
                {
                    foreach (var key in dict.Keys)
                    {
                        columns.Add(new ColumnInfo { Name = key, Type = "string" });
                    }
                    firstRow = false;
                }

                rows.Add(new Dictionary<string, object?>(dict));
            }

            stopwatch.Stop();
            return (rows, columns, stopwatch.ElapsedMilliseconds);
        }
        catch (MySqlException ex)
        {
            stopwatch.Stop();
            throw new InvalidOperationException($"Database error: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            throw new InvalidOperationException($"Query timeout after {timeoutMs}ms", ex);
        }
    }
}
