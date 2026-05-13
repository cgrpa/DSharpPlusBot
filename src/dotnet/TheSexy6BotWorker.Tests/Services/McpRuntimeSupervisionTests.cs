using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Services;

public class McpRuntimeSupervisionTests
{
    [Fact]
    public void ExponentialReconnectPolicy_UsesBackoffWithJitter_AndCapsAt60Seconds()
    {
        var jitter = new SequenceJitterProvider(0d, 1d, 0.5d, 1d);
        var policy = new ExponentialMcpReconnectDelayPolicy(jitter);

        Assert.Equal(2000, policy.GetDelay(1).TotalMilliseconds);
        Assert.Equal(4800, policy.GetDelay(2).TotalMilliseconds);
        Assert.Equal(8800, policy.GetDelay(3).TotalMilliseconds);
        Assert.Equal(60000, policy.GetDelay(6).TotalMilliseconds);
    }

    [Fact]
    public void ExponentialReconnectPolicy_ThrowsForNonPositiveAttempt()
    {
        var policy = new ExponentialMcpReconnectDelayPolicy(new SequenceJitterProvider(0d));

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.GetDelay(0));
    }

    [Fact]
    public void ExponentialReconnectPolicy_ClampsOutOfRangeJitterValues()
    {
        var policy = new ExponentialMcpReconnectDelayPolicy(new SequenceJitterProvider(-10d, 10d));

        Assert.Equal(2000, policy.GetDelay(1).TotalMilliseconds);
        Assert.Equal(4800, policy.GetDelay(2).TotalMilliseconds);
    }

    [Fact]
    public async Task InvokeAsync_RejectsToolsOutsideFixedRegisteredSurface()
    {
        var runtimeClient = new ScriptedRuntimeClient();
        var telemetrySink = new RecordingTelemetrySink();
        using var supervisor = CreateSupervisor(
            runtimeClient,
            new ExponentialMcpReconnectDelayPolicy(new SequenceJitterProvider(0d)),
            new RecordingDelayScheduler(),
            telemetrySink);

        var outcome = await supervisor.InvokeAsync(
            "TavilyRemoteMcp",
            "extract",
            new KernelArguments(),
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("currently unavailable", outcome.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, runtimeClient.ConnectCalls);
        Assert.Equal(0, runtimeClient.InvokeCalls);
        Assert.Empty(telemetrySink.Events);
    }

    [Fact]
    public async Task InvokeAsync_OnDisconnect_SchedulesReconnectAndEmitsTelemetry()
    {
        var runtimeClient = new ScriptedRuntimeClient();
        runtimeClient.EnqueueConnectResult(static () => Task.CompletedTask);
        runtimeClient.EnqueueConnectResult(static () => Task.FromException(new InvalidOperationException("temporary outage")));
        runtimeClient.EnqueueConnectResult(static () => Task.CompletedTask);
        runtimeClient.EnqueueInvokeResult(static () => Task.FromException<string>(
            new McpRuntimeDisconnectedException("socket closed\r\nsecret=abc")));

        var delayScheduler = new RecordingDelayScheduler();
        var telemetrySink = new RecordingTelemetrySink();
        using var supervisor = CreateSupervisor(
            runtimeClient,
            new ExponentialMcpReconnectDelayPolicy(new SequenceJitterProvider(0d, 0d)),
            delayScheduler,
            telemetrySink);

        var outcome = await supervisor.InvokeAsync(
            "TavilyRemoteMcp",
            "search",
            new KernelArguments(),
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("currently unavailable", outcome.Content, StringComparison.OrdinalIgnoreCase);

        await WaitForAsync(() => runtimeClient.ConnectCalls >= 3);

        Assert.Equal(1, runtimeClient.InvokeCalls);
        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)],
            delayScheduler.Delays);

        var invocation = Assert.Single(
            telemetrySink.Events.Where(e => e.Kind == McpRuntimeTelemetryEventKind.InvocationCompleted));
        Assert.Equal("TavilyRemoteMcp", invocation.PluginAlias);
        Assert.Equal("search", invocation.ToolName);
        Assert.Equal(false, invocation.IsSuccess);
        Assert.NotNull(invocation.Error);
        Assert.DoesNotContain('\n', invocation.Error!.Message);
        Assert.DoesNotContain('\r', invocation.Error!.Message);
        Assert.Equal("McpRuntimeDisconnectedException", invocation.Error.Category);

        Assert.Contains(
            telemetrySink.Events,
            e => e.Kind == McpRuntimeTelemetryEventKind.SessionReconnectScheduled
                && e.Attempt == 1
                && e.ReconnectDelayMs == 2000);
        Assert.Contains(
            telemetrySink.Events,
            e => e.Kind == McpRuntimeTelemetryEventKind.SessionReconnectScheduled
                && e.Attempt == 2
                && e.ReconnectDelayMs == 4000);
        Assert.Contains(
            telemetrySink.Events,
            e => e.Kind == McpRuntimeTelemetryEventKind.SessionConnected
                && e.Attempt == 2);
    }

    private static McpRuntimeSupervisor CreateSupervisor(
        IMcpRuntimeClient runtimeClient,
        IMcpReconnectDelayPolicy reconnectDelayPolicy,
        IMcpDelayScheduler delayScheduler,
        IMcpRuntimeTelemetrySink telemetrySink)
    {
        var options = Options.Create(new McpOptions
        {
            Enabled = true,
            Servers = new Dictionary<string, McpServerOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tavily"] = new McpServerOptions
                {
                    Endpoint = "https://mcp.tavily.com/mcp",
                    AllowedTools = ["search"],
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Authorization"] = "Bearer test"
                    }
                }
            }
        });

        var resolver = new McpServerConfigurationResolver(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>()));

        return new McpRuntimeSupervisor(
            options,
            resolver,
            new StableMcpServerPluginAliasProvider(),
            runtimeClient,
            reconnectDelayPolicy,
            delayScheduler,
            telemetrySink);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
            {
                throw new TimeoutException("Condition was not met within timeout.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class ScriptedRuntimeClient : IMcpRuntimeClient
    {
        private readonly Queue<Func<Task>> _connectResults = [];
        private readonly Queue<Func<Task<string>>> _invokeResults = [];

        public int ConnectCalls { get; private set; }

        public int InvokeCalls { get; private set; }

        public void EnqueueConnectResult(Func<Task> connectResult) => _connectResults.Enqueue(connectResult);

        public void EnqueueInvokeResult(Func<Task<string>> invokeResult) => _invokeResults.Enqueue(invokeResult);

        public Task ConnectAsync(McpRuntimeServerDescriptor server, CancellationToken cancellationToken)
        {
            ConnectCalls++;
            return _connectResults.Count == 0
                ? Task.CompletedTask
                : _connectResults.Dequeue().Invoke();
        }

        public Task<string> InvokeAsync(McpRuntimeInvocationRequest request, CancellationToken cancellationToken)
        {
            InvokeCalls++;
            return _invokeResults.Count == 0
                ? Task.FromResult("ok")
                : _invokeResults.Dequeue().Invoke();
        }
    }

    private sealed class RecordingDelayScheduler : IMcpDelayScheduler
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTelemetrySink : IMcpRuntimeTelemetrySink
    {
        public List<McpRuntimeTelemetryEvent> Events { get; } = [];

        public void Publish(McpRuntimeTelemetryEvent telemetryEvent)
        {
            Events.Add(telemetryEvent);
        }
    }

    private sealed class SequenceJitterProvider(params double[] values) : IMcpJitterProvider
    {
        private readonly Queue<double> _values = new(values);

        public double Next() => _values.Count == 0 ? 0d : _values.Dequeue();
    }

    private sealed class DictionaryEnvironmentVariableProvider(IDictionary<string, string?> values)
        : IEnvironmentVariableProvider
    {
        private readonly Dictionary<string, string?> _values = new(values, StringComparer.OrdinalIgnoreCase);

        public string? GetEnvironmentVariable(string variableName) => _values.GetValueOrDefault(variableName);
    }
}
