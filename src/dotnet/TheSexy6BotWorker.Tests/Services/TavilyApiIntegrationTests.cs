using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class TavilyApiIntegrationTests
{
    [Fact]
    public async Task TavilySearchTool_Live_WhenEnabled_ReturnsParisForCapitalOfFranceQuery()
    {
        if (!IsLiveEnabled())
        {
            return;
        }

        var service = CreateLiveService(GetRequiredApiKey());
        var result = await service.TavilySearchAsync(
            "What is the capital of France?",
            searchDepth: "basic",
            maxResults: 5,
            includeAnswer: true);

        Assert.False(IsStructuredToolError(result), $"Expected successful Tavily response. Actual: {result}");
        Assert.Contains("paris", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TavilyMapTool_Live_WhenEnabled_ReturnsPayload()
    {
        if (!IsLiveEnabled())
        {
            return;
        }

        var service = CreateLiveService(GetRequiredApiKey());
        var result = await service.TavilyMapAsync("https://example.com", maxDepth: 1);

        Assert.False(IsStructuredToolError(result), $"Expected successful Tavily response. Actual: {result}");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    private static TavilyApiService CreateLiveService(string apiKey)
    {
        var endpoint = Environment.GetEnvironmentVariable("TAVILY_API_ENDPOINT") ?? TavilyApiOptions.DefaultEndpoint;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("TavilyApiKey", apiKey)
            ])
            .Build();
        var options = Options.Create(new TavilyApiOptions
        {
            Endpoint = endpoint,
            TimeoutSeconds = 45,
            MaxRetries = 2,
            BaseDelayMilliseconds = 250,
            MaxDelayMilliseconds = 4000
        });
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint.EndsWith("/", StringComparison.Ordinal) ? endpoint : $"{endpoint}/", UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(45)
        };

        return new TavilyApiService(
            httpClient,
            configuration,
            options,
            NullLogger<TavilyApiService>.Instance,
            new Random(1234));
    }

    private static string GetRequiredApiKey()
    {
        var environmentKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey))
        {
            return environmentKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .Build();
        var userSecretKey = configuration["TavilyApiKey"];
        Assert.False(string.IsNullOrWhiteSpace(userSecretKey),
            "Tavily API key not found. Set TAVILY_API_KEY or user secret TavilyApiKey.");
        return userSecretKey!;
    }

    private static bool IsStructuredToolError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.False)
            {
                return false;
            }

            if (!root.TryGetProperty("tool", out var tool) || tool.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var toolName = tool.GetString();
            return toolName?.StartsWith("tavily_", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLiveEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_TAVILY_LIVE_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
