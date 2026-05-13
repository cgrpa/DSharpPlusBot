using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Tests.Configuration;

public class McpKernelPluginRegistrationCoordinatorTests
{
    [Fact]
    public async Task RegisterAsync_BootstrapsServersInParallel()
    {
        var options = CreateEnabledOptions(
            ("Tavily", new McpServerOptions { AllowedTools = ["search"] }),
            ("Weather", new McpServerOptions { AllowedTools = ["forecast"] }));

        var maxConcurrency = 0;
        var currentConcurrency = 0;
        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            async (_, cancellationToken) =>
            {
                var concurrency = Interlocked.Increment(ref currentConcurrency);
                UpdateMaxConcurrency(ref maxConcurrency, concurrency);

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

                Interlocked.Decrement(ref currentConcurrency);
                return McpServerToolDiscoveryResult.Success(
                [
                    new McpToolDescriptor("search"),
                    new McpToolDescriptor("forecast")
                ]);
            });

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Equal(2, result.RegisteredServers.Count);
        Assert.True(maxConcurrency >= 2, $"Expected parallel bootstrap but observed max concurrency {maxConcurrency}.");
    }

    [Fact]
    public async Task RegisterAsync_AggregatesParallelResultsAcrossServers()
    {
        var options = CreateEnabledOptions(
            ("Tavily", new McpServerOptions { AllowedTools = ["search"] }),
            ("Weather", new McpServerOptions { AllowedTools = ["forecast"] }));

        var tavilyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var weatherStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            async (request, cancellationToken) =>
            {
                if (string.Equals(request.ServerName, "Tavily", StringComparison.OrdinalIgnoreCase))
                {
                    tavilyStarted.TrySetResult();
                }
                else if (string.Equals(request.ServerName, "Weather", StringComparison.OrdinalIgnoreCase))
                {
                    weatherStarted.TrySetResult();
                }

                await release.Task.WaitAsync(cancellationToken);

                if (string.Equals(request.ServerName, "Tavily", StringComparison.OrdinalIgnoreCase))
                {
                    return McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]);
                }

                return McpServerToolDiscoveryResult.Failure("Weather unreachable");
            });

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var registrationTask = coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        await Task.WhenAll(tavilyStarted.Task, weatherStarted.Task).WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult();

        var result = await registrationTask;

        var registered = Assert.Single(result.RegisteredServers);
        Assert.Equal("Tavily", registered.ServerName);
        Assert.Equal(["search"], registered.RegisteredTools);

        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal("Weather", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.ToolDiscoveryFailed, skipped.Reason);
    }

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
            (_, _) =>
            {
                callOrder.Add(McpTransportKind.StreamableHttp);
                return Task.FromResult(McpServerToolDiscoveryResult.Failure("streamable failed"));
            });
        var sse = new FakeDiscoveryClient(
            McpTransportKind.ServerSentEvents,
            (_, _) =>
            {
                callOrder.Add(McpTransportKind.ServerSentEvents);
                return Task.FromResult(McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]));
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
            (_, _) => Task.FromResult(McpServerToolDiscoveryResult.Success(
                [
                    new McpToolDescriptor("search"),
                    new McpToolDescriptor("extract"),
                    new McpToolDescriptor("crawl")
                ])));

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
            (_, _) => Task.FromResult(McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")])));

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
    public async Task RegisterAsync_WhenStrictStartupIsDisabled_ContinuesInDegradedMode()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search"]
        }));
        options.StrictStartup = false;

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            (_, _) => Task.FromResult(McpServerToolDiscoveryResult.Failure("Server unavailable")));

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Empty(result.RegisteredServers);
        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal("Tavily", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.ToolDiscoveryFailed, skipped.Reason);
    }

    [Fact]
    public async Task RegisterAsync_WhenStrictStartupIsEnabled_FailsFast()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search"]
        }));
        options.StrictStartup = true;

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            (_, _) => Task.FromResult(McpServerToolDiscoveryResult.Failure("Server unavailable")));

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var exception = await Assert.ThrowsAsync<McpStrictStartupException>(() =>
            coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options));

        Assert.Empty(exception.RegistrationResult.RegisteredServers);
        var skipped = Assert.Single(exception.RegistrationResult.SkippedServers);
        Assert.Equal("Tavily", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.ToolDiscoveryFailed, skipped.Reason);
    }

    [Fact]
    public async Task RegisterAsync_WhenStrictStartupIsEnabled_IncludesResolverAndDiscoverySkipsInFailure()
    {
        var options = CreateEnabledOptions(
            ("Tavily", new McpServerOptions
            {
                AllowedTools = ["search"],
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Authorization"] = "Bearer ${TavilyApiKey}"
                }
            }),
            ("Weather", new McpServerOptions
            {
                AllowedTools = ["forecast"]
            }));
        options.StrictStartup = true;

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            (_, _) => Task.FromResult(McpServerToolDiscoveryResult.Failure("Server unavailable")));

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator([discovery], registrar);

        var exception = await Assert.ThrowsAsync<McpStrictStartupException>(() =>
            coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options));

        Assert.Empty(exception.RegistrationResult.RegisteredServers);
        Assert.Equal(2, exception.RegistrationResult.SkippedServers.Count);
        Assert.Contains(
            exception.RegistrationResult.SkippedServers,
            s => s.ServerName == "Tavily" && s.Reason == McpServerSkipReason.MissingInterpolatedValue);
        Assert.Contains(
            exception.RegistrationResult.SkippedServers,
            s => s.ServerName == "Weather" && s.Reason == McpServerSkipReason.ToolDiscoveryFailed);
    }

    [Fact]
    public async Task RegisterAsync_UsesDefaultStartupTimeoutWhenServerDoesNotOverride()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search"]
        }));

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                return McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]);
            });

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator(
            [discovery],
            registrar,
            defaultServerStartupTimeout: TimeSpan.FromMilliseconds(120));

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Empty(result.RegisteredServers);
        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal(McpServerSkipReason.StartupTimeout, skipped.Reason);
    }

    [Fact]
    public async Task RegisterAsync_UsesServerTimeoutOverrideWhenProvided()
    {
        var options = CreateEnabledOptions(("Tavily", new McpServerOptions
        {
            AllowedTools = ["search"],
            Startup = new McpServerStartupOptions
            {
                ConnectTimeoutSeconds = 1
            }
        }));

        var discovery = new FakeDiscoveryClient(
            McpTransportKind.StreamableHttp,
            async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
                return McpServerToolDiscoveryResult.Success([new McpToolDescriptor("search")]);
            });

        var registrar = new RecordingPluginRegistrar();
        var coordinator = CreateCoordinator(
            [discovery],
            registrar,
            defaultServerStartupTimeout: TimeSpan.FromSeconds(3));

        var result = await coordinator.RegisterAsync(new FakeKernelBuilderPlugins(), options);

        Assert.Empty(result.RegisteredServers);
        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal(McpServerSkipReason.StartupTimeout, skipped.Reason);
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
        RecordingPluginRegistrar registrar,
        TimeSpan? defaultServerStartupTimeout = null)
    {
        var resolver = new McpServerConfigurationResolver(
            BuildConfiguration(new Dictionary<string, string?>()),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>()));

        return new McpKernelPluginRegistrationCoordinator(
            resolver,
            discoveryClients,
            new StableMcpServerPluginAliasProvider(),
            registrar,
            defaultServerStartupTimeout);
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
        Func<McpServerToolDiscoveryRequest, CancellationToken, Task<McpServerToolDiscoveryResult>> discoverAsync)
        : IMcpServerToolDiscoveryClient
    {
        public McpTransportKind TransportKind { get; } = transportKind;

        public Task<McpServerToolDiscoveryResult> DiscoverToolsAsync(
            McpServerToolDiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            return discoverAsync(request, cancellationToken);
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

    private static void UpdateMaxConcurrency(ref int maxConcurrency, int observedConcurrency)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref maxConcurrency);
            if (observedConcurrency <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref maxConcurrency, observedConcurrency, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}
