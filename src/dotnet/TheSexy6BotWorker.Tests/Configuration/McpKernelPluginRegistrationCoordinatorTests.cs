using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Tests.Configuration;

public class McpKernelPluginRegistrationCoordinatorTests
{
    [Fact]
    public async Task RegisterAsync_TriesStreamableHttpFirstThenFallsBackToSse()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            Endpoint = "https://mcp.tavily.com/mcp",
            AllowedTools = ["search"]
        }));

        var callOrder = new List<McpTransportKind>();
        var streamable = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            _ =>
            {
                callOrder.Add(McpTransportKind.StreamableHttp);
                return McpServerToolDiscoveryResult.Failure("streamable failed");
            });
        var sse = new FakeDiscoveryClient(
            McpTransportKind.ServerSentEvents,
            _ =>
            {
                callOrder.Add(McpTransportKind.ServerSentEvents);
                return McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]);
            });

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([sse, streamable], registrar);

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Equal(
            [McpTransportKind.StreamableHttp, McpTransportKind.ServerSentEvents],
            callOrder);

        var registration = Assert.Single(result.RegisteredServers);
        Assert.Equal("Tavily", registration.ServerName);
        Assert.Equal("TavilyRemoteMcp", registration.PluginAlias);
        Assert.Equal(nameof(McpTransportKind.ServerSentEvents), registration.Transport);
        Assert.Equal(["search"], registration.RegisteredTools);

        var recorded = Assert.Single(registrar.Registrations);
        Assert.Equal("TavilyRemoteMcp", recorded.PluginAlias);
        Assert.Equal(["search"], recorded.ToolNames);
    }

    [Fact]
    public async Task RegisterAsync_RegistersOnlyConfiguredAllowedTools()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search"]
        }));

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            _ => McpServerToolDiscoveryResult.Success(
            [
                new McpToolDescriptor("search"),
                new McpToolDescriptor("extract"),
                new McpToolDescriptor("crawl")
            ]));

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Empty(result.SkippedServers);
        var registration = Assert.Single(result.RegisteredServers);
        Assert.Equal(["search"], registration.RegisteredTools);

        var recorded = Assert.Single(registrar.Registrations);
        Assert.Equal(["search"], recorded.ToolNames);
    }

    [Fact]
    public async Task RegisterAsync_SkipsServerWhenAllowedToolIsMissingFromDiscovery()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search", "extract"]
        }));

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            _ => McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]));

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Empty(result.RegisteredServers);
        Assert.Empty(registrar.Registrations);

        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal("Tavily", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.MissingAllowedTools, skipped.Reason);
        Assert.Equal(["extract"], skipped.MissingInterpolationKeys);
    }

    [Fact]
    public void StableMcpServerPluginAliasProvider_UsesStableTavilyAlias_AndDeterministicFallback()
    {
        var aliasProvider = new StableMcpServerPluginAliasProvider();

        Assert.Equal("TavilyRemoteMcp", aliasProvider.GetPluginAlias("Tavily"));
        Assert.Equal("TavilyRemoteMcp", aliasProvider.GetPluginAlias("tAvIlY"));
        Assert.Equal("AcmeSearch1RemoteMcp", aliasProvider.GetPluginAlias("Acme Search-1"));
    }

    private static McpKernelPluginRegistrationCoordinator CreateCoordinator(
        IEnumerable<IMcpServerToolDiscoveryClient> discoveryClients,
        RecordingPluginRegistrar registrar)
    {
        var resolver = new McpServerConfigurationResolver(
            BuildConfiguration(new Dictionary<string, string?>()),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>()));

        return new McpKernelPluginRegistrationCoordinator(
            resolver,
            discoveryClients,
            new StableMcpServerPluginAliasProvider(),
            registrar);
    }

    private static McpOptions CreateEnabledOptions(params (string Name, McpServerOptions Server)[] servers)
    {
        var options = new McpOptions
        {
            Enabled = true
        };

        foreach (var (name, server) in servers)
        {
            options.Servers[name] = server;
        }

        return options;
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class FakeDiscoveryClient(
        McpTransportKind transportKind,
        Func<McpServerToolDiscoveryRequest, McpServerToolDiscoveryResult> discover)
        : IMcpServerToolDiscoveryClient
    {
        public McpTransportKind TransportKind { get; } = transportKind;

        public Task<McpServerToolDiscoveryResult> DiscoverToolsAsync(
            McpServerToolDiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(discover(request));
        }
    }

    private sealed class RecordingPluginRegistrar : IMcpKernelPluginRegistrar
    {
        public List<(string PluginAlias, string ServerName, IReadOnlyList<string> ToolNames)> Registrations { get; } = [];

        public void RegisterAllowedTools(
            IKernelBuilderPlugins plugins,
            string pluginAlias,
            string serverName,
            IReadOnlyList<McpToolDescriptor> allowedTools)
        {
            Registrations.Add((
                pluginAlias,
                serverName,
                allowedTools.Select(static t => t.Name).ToArray()));
        }
    }

    private sealed class DictionaryEnvironmentVariableProvider(IDictionary<string, string?> values)
        : IEnvironmentVariableProvider
    {
        private readonly Dictionary<string, string?> _values = new(values, StringComparer.OrdinalIgnoreCase);

        public string? GetEnvironmentVariable(string variableName) => _values.GetValueOrDefault(variableName);
    }

    private sealed class FakeKernelBuilderPlugins : IKernelBuilderPlugins
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
    }
}
