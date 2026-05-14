using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Services;

public sealed class TavilyApiService
{
    private const string ApiKeyConfigurationKey = "TavilyApiKey";
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ErrorJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TavilyApiOptions _options;
    private readonly ILogger<TavilyApiService> _logger;
    private readonly Random _random;

    public TavilyApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<TavilyApiOptions> options,
        ILogger<TavilyApiService> logger,
        Random? random = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = random ?? Random.Shared;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeEndpoint(_options.Endpoint);
        }

        var timeoutSeconds = Math.Max(5, _options.TimeoutSeconds);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    [KernelFunction("tavily_search")]
    [Description("Searches the web with Tavily and returns raw Tavily JSON.")]
    public Task<string> TavilySearchAsync(
        [Description("The search query to execute.")] string query,
        [Description("Search depth mode. Usually 'basic' or 'advanced'.")] string searchDepth = "basic",
        [Description("Maximum number of search results to return.")] int maxResults = 5,
        [Description("Whether to include Tavily's generated answer field.")] bool includeAnswer = true,
        [Description("Optional topic hint such as 'general' or 'news'.")] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["search_depth"] = searchDepth,
            ["max_results"] = Math.Clamp(maxResults, 1, 20),
            ["include_answer"] = includeAnswer
        };

        if (!string.IsNullOrWhiteSpace(topic))
        {
            payload["topic"] = topic;
        }

        return InvokeEndpointAsync("tavily_search", "search", payload, cancellationToken);
    }

    [KernelFunction("tavily_extract")]
    [Description("Extracts content from one or more URLs and returns raw Tavily JSON.")]
    public Task<string> TavilyExtractAsync(
        [Description("One or more URLs, separated by commas, spaces, or newlines.")] string urls,
        [Description("Whether to include image URLs in extracted content.")] bool includeImages = false,
        [Description("Whether to include raw content when available.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["urls"] = ParseUrls(urls),
            ["include_images"] = includeImages,
            ["include_raw_content"] = includeRawContent
        };

        return InvokeEndpointAsync("tavily_extract", "extract", payload, cancellationToken);
    }

    [KernelFunction("tavily_crawl")]
    [Description("Crawls a URL and returns raw Tavily JSON.")]
    public Task<string> TavilyCrawlAsync(
        [Description("The URL to crawl.")] string url,
        [Description("Maximum crawl depth.")] int maxDepth = 1,
        [Description("Maximum breadth per crawl level.")] int maxBreadth = 20,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["url"] = url,
            ["max_depth"] = Math.Clamp(maxDepth, 1, 5),
            ["max_breadth"] = Math.Clamp(maxBreadth, 1, 50)
        };

        return InvokeEndpointAsync("tavily_crawl", "crawl", payload, cancellationToken);
    }

    [KernelFunction("tavily_map")]
    [Description("Maps discoverable links from a URL and returns raw Tavily JSON.")]
    public Task<string> TavilyMapAsync(
        [Description("The URL to map.")] string url,
        [Description("Maximum map depth.")] int maxDepth = 1,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["url"] = url,
            ["max_depth"] = Math.Clamp(maxDepth, 1, 5)
        };

        return InvokeEndpointAsync("tavily_map", "map", payload, cancellationToken);
    }

    private async Task<string> InvokeEndpointAsync(
        string toolName,
        string relativePath,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration[ApiKeyConfigurationKey];
        var endpoint = new Uri(_httpClient.BaseAddress!, relativePath).ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return SerializeError(
                toolName,
                endpoint,
                httpStatus: null,
                error: $"Missing required configuration key '{ApiKeyConfigurationKey}'.",
                retryable: false,
                attempt: 0,
                correlationId: null,
                traceId: null);
        }

        payload["api_key"] = apiKey;
        var maxAttempts = Math.Max(1, _options.MaxRetries + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await _httpClient
                    .PostAsJsonAsync(relativePath, payload, RequestJsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                var retryable = IsRetryableStatusCode(response.StatusCode);
                if (retryable && attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var (correlationId, traceId) = GetCorrelationMetadata(response);
                var message = $"Tavily request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {Truncate(body)}";
                return SerializeError(toolName, endpoint, (int)response.StatusCode, message, retryable, attempt, correlationId, traceId);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return SerializeError(
                    toolName,
                    endpoint,
                    httpStatus: null,
                    error: $"Tavily request timed out: {ex.Message}",
                    retryable: true,
                    attempt: attempt,
                    correlationId: null,
                    traceId: null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Transient Tavily HTTP failure for {ToolName} attempt {Attempt}/{MaxAttempts}.", toolName, attempt, maxAttempts);
                if (attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return SerializeError(
                    toolName,
                    endpoint,
                    httpStatus: null,
                    error: $"Tavily request failed: {ex.Message}",
                    retryable: true,
                    attempt: attempt,
                    correlationId: null,
                    traceId: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Non-retryable Tavily tool execution error for {ToolName}.", toolName);
                return SerializeError(
                    toolName,
                    endpoint,
                    httpStatus: null,
                    error: $"Unexpected Tavily execution error: {ex.Message}",
                    retryable: false,
                    attempt: attempt,
                    correlationId: null,
                    traceId: null);
            }
        }

        return SerializeError(
            toolName,
            endpoint,
            httpStatus: null,
            error: "Unexpected Tavily execution flow termination.",
            retryable: false,
            attempt: maxAttempts,
            correlationId: null,
            traceId: null);
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return numeric == 429 || (numeric >= 500 && numeric <= 599);
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseDelayMs = Math.Max(0, _options.BaseDelayMilliseconds);
        var maxDelayMs = Math.Max(baseDelayMs, _options.MaxDelayMilliseconds);
        if (baseDelayMs == 0 || maxDelayMs == 0)
        {
            return;
        }

        var exponential = (int)Math.Min(maxDelayMs, baseDelayMs * Math.Pow(2, Math.Max(0, attempt - 1)));
        var jitterBound = Math.Max(1, exponential / 2);
        var jitter = _random.Next(0, jitterBound + 1);
        var delay = TimeSpan.FromMilliseconds(Math.Min(maxDelayMs, exponential + jitter));
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private static Uri NormalizeEndpoint(string endpoint)
    {
        var raw = string.IsNullOrWhiteSpace(endpoint) ? TavilyApiOptions.DefaultEndpoint : endpoint.Trim();
        if (!raw.EndsWith("/", StringComparison.Ordinal))
        {
            raw += "/";
        }

        return new Uri(raw, UriKind.Absolute);
    }

    private static string SerializeError(
        string toolName,
        string endpoint,
        int? httpStatus,
        string error,
        bool retryable,
        int attempt,
        string? correlationId,
        string? traceId)
    {
        var payload = new TavilyToolErrorPayload
        {
            Success = false,
            Tool = toolName,
            Endpoint = endpoint,
            HttpStatus = httpStatus,
            Error = error,
            Retryable = retryable,
            Attempt = attempt,
            CorrelationId = correlationId,
            TraceId = traceId
        };

        return JsonSerializer.Serialize(payload, ErrorJsonOptions);
    }

    private static (string? CorrelationId, string? TraceId) GetCorrelationMetadata(HttpResponseMessage response)
    {
        static string? TryGet(HttpResponseHeaders headers, string key)
        {
            if (!headers.TryGetValues(key, out var values))
            {
                return null;
            }

            return values.FirstOrDefault();
        }

        var correlationId = TryGet(response.Headers, "x-request-id")
                            ?? TryGet(response.Headers, "request-id")
                            ?? TryGet(response.Headers, "x-correlation-id");
        var traceId = TryGet(response.Headers, "traceparent");
        return (correlationId, traceId);
    }

    private static string[] ParseUrls(string urls)
    {
        return urls
            .Split([',', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Truncate(string value, int maxLength = 700)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private sealed class TavilyToolErrorPayload
    {
        public bool Success { get; init; }
        public string Tool { get; init; } = string.Empty;
        public string Endpoint { get; init; } = string.Empty;
        public int? HttpStatus { get; init; }
        public string Error { get; init; } = string.Empty;
        public bool Retryable { get; init; }
        public int Attempt { get; init; }
        public string? CorrelationId { get; init; }
        public string? TraceId { get; init; }
    }
}
