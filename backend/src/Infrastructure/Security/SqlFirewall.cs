using System.Text.RegularExpressions;
using Application.Abstractions;
using Contracts.Agent;

namespace Infrastructure.Security;

/// <summary>
/// SQL firewall: validates, rewrites, and injects tenant predicates
/// Security gates: SELECT-only, table allowlist, tenant predicate injection, LIMIT enforcement
/// </summary>
public sealed partial class SqlFirewall : ISqlFirewall
{
    [GeneratedRegex(@"^\s*select\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SelectStartRegex();

    [GeneratedRegex(@"\b(insert|update|delete|drop|alter|truncate|create|replace|grant|revoke|call|exec|execute)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ForbiddenMutationKeywordRegex();

    [GeneratedRegex(@"\b(from|join)\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FromJoinTableRegex();

    [GeneratedRegex(@"\blimit\s+\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LimitRegex();

    [GeneratedRegex(@"\blimit\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LimitValueRegex();

    [GeneratedRegex(@"\bwhere\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WhereClauseRegex();

    [GeneratedRegex(@"\bfrom\s+([a-zA-Z_][a-zA-Z0-9_]*)\s+(?:AS\s+)?([a-zA-Z_][a-zA-Z0-9_]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FromTableAliasRegex();

    [GeneratedRegex(@"\bjoin\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JoinKeywordRegex();

    public FirewallResult ValidateAndRewrite(string sqlCandidate, string customerId, SchemaPolicy policy)
    {
        var violations = new List<string>();
        var ruleHits = new List<string>();

        // Rule R000: Reject empty SQL
        if (string.IsNullOrWhiteSpace(sqlCandidate))
        {
            violations.Add("Empty SQL candidate");
            return Reject("Empty or null SQL candidate.", ruleHits, violations);
        }

        ruleHits.Add("R000_NON_EMPTY");

        // Rule R001: SELECT-only enforcement
        if (!SelectStartRegex().IsMatch(sqlCandidate))
        {
            violations.Add("Non-SELECT query detected");
            return Reject("Only SELECT queries are allowed.", ruleHits, violations);
        }

        if (ForbiddenMutationKeywordRegex().IsMatch(sqlCandidate))
        {
            violations.Add("Mutation keyword detected");
            return Reject("Mutation operations (INSERT/UPDATE/DELETE/etc) are not allowed.", ruleHits, violations);
        }

        ruleHits.Add("R001_SINGLE_SELECT_ONLY");

        // Rule R002: Multi-statement detection
        var statementCount = sqlCandidate.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        if (statementCount > 1)
        {
            violations.Add("Multiple statements detected");
            return Reject("Multiple SQL statements are not allowed.", ruleHits, violations);
        }

        ruleHits.Add("R002_NO_MULTI_STATEMENT");

        // Rule R003: Table allowlist validation
        var usedTables = FromJoinTableRegex().Matches(sqlCandidate)
            .Select(m => m.Groups[2].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var disallowedTables = usedTables
            .Where(t => !policy.AllowedTables.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (disallowedTables.Any())
        {
            violations.Add($"Disallowed tables: {string.Join(", ", disallowedTables)}");
            return Reject($"Query references non-allowlisted tables: {string.Join(", ", disallowedTables)}", ruleHits, violations);
        }

        ruleHits.Add("R003_TABLE_ALLOWLIST");

        // Rule R004: JOIN depth check
        var joinCount = JoinKeywordRegex().Matches(sqlCandidate).Count;
        if (joinCount > policy.MaxJoinDepth)
        {
            violations.Add($"Join depth {joinCount} exceeds max {policy.MaxJoinDepth}");
            return Reject($"Query exceeds maximum join depth of {policy.MaxJoinDepth}.", ruleHits, violations);
        }

        ruleHits.Add("R004_JOIN_DEPTH_OK");

        // Rule R009: Forbidden pattern detection
        foreach (var pattern in policy.ForbiddenPatterns)
        {
            if (sqlCandidate.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"Forbidden pattern: {pattern}");
                return Reject($"Query contains forbidden pattern: {pattern}", ruleHits, violations);
            }
        }

        ruleHits.Add("R009_NO_FORBIDDEN_PATTERNS");

        // Rule R006: Tenant predicate injection
        var rewritten = InjectTenantPredicate(sqlCandidate, policy.TenantColumn, customerId);
        ruleHits.Add("R006_TENANT_PREDICATE_ENFORCED");

        // Rule R007: LIMIT enforcement
        if (!LimitRegex().IsMatch(rewritten))
        {
            rewritten += $" LIMIT {policy.DefaultLimit}";
            ruleHits.Add("R007_LIMIT_INJECTED");
        }
        else
        {
            var limitMatch = LimitValueRegex().Match(rewritten);
            if (limitMatch.Success
                && int.TryParse(limitMatch.Groups[1].Value, out var requestedLimit)
                && requestedLimit > policy.MaxLimit)
            {
                rewritten = LimitValueRegex().Replace(rewritten, $"LIMIT {policy.MaxLimit}", 1);
                ruleHits.Add("R007_LIMIT_CAPPED");
            }
            else
            {
                ruleHits.Add("R007_LIMIT_PRESENT");
            }
        }

        return new FirewallResult
        {
            Approved = true,
            Status = "approved",
            RewrittenSql = rewritten,
            RuleHits = ruleHits,
            Violations = Array.Empty<string>()
        };
    }

    private static string InjectTenantPredicate(string sql, string tenantColumn, string customerId)
    {
        // Extract the main table name and alias from the FROM clause
        var fromMatch = FromTableAliasRegex().Match(sql);
        string tableQualifier = tenantColumn; // default to unqualified

        if (fromMatch.Success)
        {
            // If there's an alias (group 2), use it. Otherwise use the table name (group 1)
            var alias = fromMatch.Groups[2].Success ? fromMatch.Groups[2].Value : fromMatch.Groups[1].Value;
            tableQualifier = $"{alias}.{tenantColumn}";
        }

        var tenantPredicate = $" {tableQualifier} = @customerId";
        var hasWhere = WhereClauseRegex().IsMatch(sql);

        return hasWhere
            ? WhereClauseRegex().Replace(sql, $"WHERE{tenantPredicate} AND", 1)
            : $"{sql.TrimEnd()} WHERE{tenantPredicate}";
    }

    private static FirewallResult Reject(string reason, List<string> ruleHits, List<string> violations)
    {
        return new FirewallResult
        {
            Approved = false,
            Status = "rejected",
            Reason = reason,
            RuleHits = ruleHits,
            Violations = violations
        };
    }
}
