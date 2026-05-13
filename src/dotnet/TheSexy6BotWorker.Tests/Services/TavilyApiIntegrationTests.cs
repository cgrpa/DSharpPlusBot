using System.Net.Http.Json;
using System.Text.Json;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class TavilyApiIntegrationTests
{
    [Fact]
    public async Task TavilySearchApi_Live_WhenEnabled_ReturnsResults()
    {
        if (!IsLiveEnabled())
        {
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey));

        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.tavily.com/")
        };

        var payload = new
        {
            api_key = apiKey,
            query = "latest weather in London",
            search_depth = "basic",
            max_results = 3
        };

        using var response = await client.PostAsJsonAsync("search", payload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Tavily request failed ({(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("results", out var results));
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.True(results.GetArrayLength() > 0, "Expected Tavily to return at least one search result.");
    }

    private static bool IsLiveEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_TAVILY_LIVE_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
