using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Configuration;

public class McpToolUnavailableBehaviorTests
{
    [Fact]
    public async Task UnavailableMcpToolInvoker_ReturnsExplicitUnavailableMessage()
    {
        var invoker = new UnavailableMcpToolInvoker();

        var result = await invoker.InvokeAsync(
            "TavilyRemoteMcp",
            "search",
            new KernelArguments(),
            CancellationToken.None);

        Assert.Equal(
            "MCP tool 'search' via plugin 'TavilyRemoteMcp' is currently unavailable. This call failed and no non-MCP fallback was executed.",
            result);
    }

    [Fact]
    public async Task NoOpMcpRuntimeClient_ReturnsExplicitUnavailableMessage()
    {
        var runtimeClient = new NoOpMcpRuntimeClient();
        var descriptor = new McpRuntimeServerDescriptor
        {
            ServerName = "Tavily",
            PluginAlias = "TavilyRemoteMcp",
            Endpoint = "https://mcp.tavily.com/mcp",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            AllowedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        var result = await runtimeClient.InvokeAsync(
            new McpRuntimeInvocationRequest
            {
                Server = descriptor,
                ToolName = "search",
                Arguments = new KernelArguments()
            },
            CancellationToken.None);

        Assert.Equal(
            "MCP tool 'search' via plugin 'TavilyRemoteMcp' is currently unavailable. This call failed and no non-MCP fallback was executed.",
            result);
    }

    [Fact]
    public async Task RuntimeSupervisor_WithMcpDisabled_RejectsToolInvocationExplicitly()
    {
        var options = Options.Create(new McpOptions { Enabled = false });
        var resolver = new McpServerConfigurationResolver(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            new ProcessEnvironmentVariableProvider());

        using var supervisor = new McpRuntimeSupervisor(
            options,
            resolver,
            new StableMcpServerPluginAliasProvider(),
            new NoOpMcpRuntimeClient(),
            new ExponentialMcpReconnectDelayPolicy(new RandomMcpJitterProvider()),
            new SystemMcpDelayScheduler(),
            new RecordingTelemetrySink());

        var outcome = await supervisor.InvokeAsync(
            "TavilyRemoteMcp",
            "search",
            new KernelArguments(),
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            "MCP tool 'search' via plugin 'TavilyRemoteMcp' is currently unavailable. This call failed and no non-MCP fallback was executed.",
            outcome.Content);
    }

    private sealed class RecordingTelemetrySink : IMcpRuntimeTelemetrySink
    {
        public void Publish(McpRuntimeTelemetryEvent telemetryEvent)
        {
        }
    }
}
