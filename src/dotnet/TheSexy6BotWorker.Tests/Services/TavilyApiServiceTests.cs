using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Services;

public class TavilyApiServiceTests
{
    [Fact]
    public async Task TavilySearchAsync_OnSuccess_ReturnsRawJsonAndBuildsExpectedPayload()
    {
        const string responsePayload = """{"results":[{"title":"Paris"}]}""";
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload)
            }
        ]);
        using var scope = CreateService(handler);
        var service = scope.Service;

        var result = await service.TavilySearchAsync("What is the capital of France?");

        Assert.Equal(responsePayload, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/search", request.Path);

        using var bodyDocument = JsonDocument.Parse(request.Body);
        var root = bodyDocument.RootElement;
        Assert.Equal("test-api-key", root.GetProperty("api_key").GetString());
        Assert.Equal("What is the capital of France?", root.GetProperty("query").GetString());
        Assert.Equal("basic", root.GetProperty("search_depth").GetString());
        Assert.Equal(5, root.GetProperty("max_results").GetInt32());
        Assert.True(root.GetProperty("include_answer").GetBoolean());
    }

    [Fact]
    public async Task TavilySearchAsync_RetriesOn429_ThenReturnsSuccess()
    {
        const string successPayload = """{"results":[{"title":"Paris"}]}""";
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("""{"detail":"rate limited"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successPayload)
            }
        ]);
        using var scope = CreateService(handler, maxRetries: 2);
        var service = scope.Service;

        var result = await service.TavilySearchAsync("capital of france");

        Assert.Equal(successPayload, result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task TavilySearchAsync_On400_ReturnsStructuredErrorWithoutRetry()
    {
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"detail":"invalid query"}""")
            }
        ]);
        using var scope = CreateService(handler, maxRetries: 3);
        var service = scope.Service;

        var result = await service.TavilySearchAsync(string.Empty);

        Assert.Single(handler.Requests);

        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("tavily_search", root.GetProperty("tool").GetString());
        Assert.Equal(400, root.GetProperty("httpStatus").GetInt32());
        Assert.False(root.GetProperty("retryable").GetBoolean());
        Assert.Equal(1, root.GetProperty("attempt").GetInt32());
    }

    [Fact]
    public async Task TavilySearchAsync_OnNetworkFailure_RetriesThenReturnsStructuredError()
    {
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpRequestException("socket closed"),
            new HttpRequestException("socket closed")
        ]);
        using var scope = CreateService(handler, maxRetries: 1);
        var service = scope.Service;

        var result = await service.TavilySearchAsync("capital of france");

        Assert.Equal(2, handler.Requests.Count);

        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("tavily_search", root.GetProperty("tool").GetString());
        Assert.True(root.GetProperty("retryable").GetBoolean());
        Assert.Equal(2, root.GetProperty("attempt").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("httpStatus").ValueKind);
    }

    [Fact]
    public async Task TavilyExtractAsync_ParsesCommaAndNewlineSeparatedUrls()
    {
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""")
            }
        ]);
        using var scope = CreateService(handler);
        var service = scope.Service;

        await service.TavilyExtractAsync("https://example.com, https://example.org\nhttps://example.com");

        var request = Assert.Single(handler.Requests);
        using var bodyDocument = JsonDocument.Parse(request.Body);
        var urls = bodyDocument.RootElement
            .GetProperty("urls")
            .EnumerateArray()
            .Select(x => x.GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(["https://example.com", "https://example.org"], urls);
    }

    [Fact]
    public async Task TavilyMapAsync_WhenApiKeyMissing_ReturnsStructuredError()
    {
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""")
            }
        ]);
        using var scope = CreateService(handler, includeApiKey: false);
        var service = scope.Service;

        var result = await service.TavilyMapAsync("https://example.com");

        Assert.Empty(handler.Requests);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("retryable").GetBoolean());
        Assert.Equal(0, root.GetProperty("attempt").GetInt32());
    }

    [Fact]
    public void TavilyApiPlugin_RegistersExpectedToolNames()
    {
        var handler = new ScriptedHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""")
            }
        ]);
        using var scope = CreateService(handler);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(scope.Service, "TavilyApi");
        var kernel = kernelBuilder.Build();
        var plugin = kernel.Plugins.First(p => p.Name == "TavilyApi");
        var toolNames = plugin.Select(function => function.Name).OrderBy(name => name).ToArray();

        Assert.Equal(["tavily_crawl", "tavily_extract", "tavily_map", "tavily_search"], toolNames);
    }

    private static DisposableService CreateService(
        ScriptedHttpMessageHandler handler,
        int maxRetries = 2,
        bool includeApiKey = true)
    {
        var configurationEntries = includeApiKey
            ? new Dictionary<string, string?> { ["TavilyApiKey"] = "test-api-key" }
            : new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationEntries)
            .Build();
        var options = Options.Create(new TavilyApiOptions
        {
            Endpoint = TavilyApiOptions.DefaultEndpoint,
            TimeoutSeconds = 30,
            MaxRetries = maxRetries,
            BaseDelayMilliseconds = 0,
            MaxDelayMilliseconds = 0
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.tavily.com/")
        };
        var service = new TavilyApiService(
            client,
            configuration,
            options,
            NullLogger<TavilyApiService>.Instance,
            new Random(42));
        return new DisposableService(service, client);
    }

    private sealed class DisposableService(TavilyApiService service, HttpClient client) : IDisposable
    {
        public TavilyApiService Service => service;

        public void Dispose()
        {
            client.Dispose();
        }
    }

    private sealed class ScriptedHttpMessageHandler(IEnumerable<object> scriptedSteps) : HttpMessageHandler
    {
        private readonly Queue<object> _scriptedSteps = new(scriptedSteps);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                body));

            if (_scriptedSteps.Count == 0)
            {
                throw new InvalidOperationException("No scripted response is available.");
            }

            var step = _scriptedSteps.Dequeue();
            if (step is Exception exception)
            {
                throw exception;
            }

            if (step is HttpResponseMessage response)
            {
                return response;
            }

            throw new InvalidOperationException($"Unsupported scripted step type: {step.GetType().Name}");
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Body);
}
