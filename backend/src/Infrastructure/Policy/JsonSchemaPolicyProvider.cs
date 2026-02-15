using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Policy;

/// <summary>
/// Loads schema policy from JSON file
/// </summary>
public sealed class JsonSchemaPolicyProvider : ISchemaPolicyProvider
{
    private readonly Lazy<SchemaPolicy> _cachedPolicy;

    public JsonSchemaPolicyProvider()
    {
        _cachedPolicy = new Lazy<SchemaPolicy>(LoadPolicyFromFile);
    }

    public SchemaPolicy GetPolicy() => _cachedPolicy.Value;

    private static SchemaPolicy LoadPolicyFromFile()
    {
        // Look for policy file - first in the app directory (Docker), then relative path (local dev)
        var policyPath = Path.Combine(AppContext.BaseDirectory, "policy", "schema-policy.json");

        if (!File.Exists(policyPath))
        {
            // Fallback for local development
            policyPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "../../../../db/policy/schema-policy.json"));
        }

        if (!File.Exists(policyPath))
        {
            throw new InvalidOperationException(
                $"Schema policy file not found. Searched: {AppContext.BaseDirectory}/policy/schema-policy.json");
        }

        try
        {
            var json = File.ReadAllText(policyPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new SchemaPolicy
            {
                Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0",
                Dialect = root.TryGetProperty("dialect", out var d) ? d.GetString() ?? "mysql8" : "mysql8",
                SelectOnly = root.TryGetProperty("selectOnly", out var so) && so.GetBoolean(),
                NoViews = root.TryGetProperty("noViews", out var nv) && nv.GetBoolean(),
                DefaultLimit = root.TryGetProperty("limitPolicy", out var lp) && lp.TryGetProperty("default", out var dl)
                    ? dl.GetInt32() : 50,
                MaxLimit = lp.TryGetProperty("max", out var ml) ? ml.GetInt32() : 200,
                TimeoutPolicyMs = root.TryGetProperty("timeoutPolicyMs", out var tp) ? tp.GetInt32() : 5000,
                TenantColumn = root.TryGetProperty("tenantColumn", out var tc) ? tc.GetString() ?? "CustomerId" : "CustomerId",
                MaxJoinDepth = root.TryGetProperty("maxJoinDepth", out var mjd) ? mjd.GetInt32() : 4,
                TenantRootTables = ParseStringArray(root, "tenantRootTables"),
                GlobalReferenceTables = ParseStringArray(root, "globalReferenceTables"),
                AllowedTables = ParseStringArray(root, "allowedTables"),
                AllowedJoinEdges = ParseJoinEdges(root),
                AllowedFunctions = ParseStringArray(root, "allowedFunctions"),
                ForbiddenPatterns = ParseStringArray(root, "forbiddenPatterns")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load schema policy from {policyPath}: {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return Array.Empty<string>();

        return element.EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<JoinEdge> ParseJoinEdges(JsonElement root)
    {
        if (!root.TryGetProperty("allowedJoinEdges", out var edgesElement))
            return Array.Empty<JoinEdge>();

        return edgesElement.EnumerateArray()
            .Select(edge => new JoinEdge
            {
                From = edge.TryGetProperty("from", out var f) ? f.GetString() ?? string.Empty : string.Empty,
                FromColumn = edge.TryGetProperty("fromColumn", out var fc) ? fc.GetString() ?? string.Empty : string.Empty,
                To = edge.TryGetProperty("to", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                ToColumn = edge.TryGetProperty("toColumn", out var tc) ? tc.GetString() ?? string.Empty : string.Empty,
                RequireTenantMatch = !edge.TryGetProperty("requireTenantMatch", out var rtm) || rtm.GetBoolean()
            })
            .ToArray();
    }
}
