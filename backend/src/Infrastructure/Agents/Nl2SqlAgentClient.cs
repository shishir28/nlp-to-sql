using System.Net.Http.Json;
using Application.Abstractions;
using Contracts.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Agents;

/// <summary>
/// HTTP client for Python LangGraph agent service
/// </summary>
public sealed class Nl2SqlAgentClient : INl2SqlAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<Nl2SqlAgentClient> _logger;

    public Nl2SqlAgentClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<Nl2SqlAgentClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Nl2SqlAgent");
        _baseUrl = configuration["AgentService:BaseUrl"]
            ?? throw new InvalidOperationException("AgentService:BaseUrl not configured");
        _logger = logger;
    }

    public async Task<Nl2SqlGenerateResponse> GenerateAsync(Nl2SqlGenerateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_baseUrl}/v1/nl2sql/generate";
            _logger.LogInformation("Calling agent service: {Endpoint}", endpoint);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Nl2SqlGenerateResponse>();
            if (result == null)
            {
                _logger.LogError("Agent service returned null response");
                return CreateFallbackClarificationResponse(request.Question);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Agent service HTTP error: {Message}", ex.Message);
            return CreateFallbackClarificationResponse(request.Question);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling agent service: {Message}", ex.Message);
            return CreateFallbackClarificationResponse(request.Question);
        }
    }

    private static Nl2SqlGenerateResponse CreateFallbackClarificationResponse(string question)
    {
        return new Nl2SqlGenerateResponse
        {
            SqlCandidate = string.Empty,
            Confidence = 0.0,
            NeedsClarification = true,
            ClarificationPrompt = "I'm unable to generate SQL at the moment. Please try rephrasing your question or contact support.",
            Reasoning = "Agent service unavailable or returned error",
            OriginalQuestion = question,
            DetectedDomain = "unknown",
            ScopedTables = Array.Empty<string>()
        };
    }
}

/// <summary>
/// HTTP client for Python result summarization endpoint
/// </summary>
public sealed class SummarizationClient : ISummarizationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<SummarizationClient> _logger;

    public SummarizationClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SummarizationClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Nl2SqlAgent");
        _baseUrl = configuration["AgentService:BaseUrl"]
            ?? throw new InvalidOperationException("AgentService:BaseUrl not configured");
        _logger = logger;
    }

    public async Task<string?> SummarizeAsync(Nl2SqlSummarizeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_baseUrl}/v1/nl2sql/summarize";
            _logger.LogInformation("Calling summarization service: {Endpoint}", endpoint);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Nl2SqlSummarizeResponse>(cancellationToken: cancellationToken);
            return result?.NlSummary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Summarization service error (non-fatal): {Message}", ex.Message);
            return null; // Summarization is best-effort — don't fail the main response
        }
    }
}
