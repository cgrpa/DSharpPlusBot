using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Services;

public enum McpRuntimeConnectionState
{
    Disconnected = 1,
    Connecting = 2,
    Connected = 3,
    Reconnecting = 4
}

public sealed class McpRuntimeServerDescriptor
{
    public required string ServerName { get; init; }

    public required string PluginAlias { get; init; }

    public required string Endpoint { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public required IReadOnlySet<string> AllowedTools { get; init; }
}

public sealed class McpRuntimeInvocationRequest
{
    public required McpRuntimeServerDescriptor Server { get; init; }

    public required string ToolName { get; init; }

    public required KernelArguments Arguments { get; init; }
}

public sealed class McpRuntimeInvocationOutcome
{
    public required bool IsSuccess { get; init; }

    public required string Content { get; init; }
}

public sealed class McpRuntimeDisconnectedException : Exception
{
    public McpRuntimeDisconnectedException(string message)
        : base(message)
    {
    }
}

public interface IMcpRuntimeClient
{
    Task ConnectAsync(McpRuntimeServerDescriptor server, CancellationToken cancellationToken);

    Task<string> InvokeAsync(McpRuntimeInvocationRequest request, CancellationToken cancellationToken);
}

public interface IMcpRuntimeSupervisor
{
    IReadOnlyList<McpRuntimeServerDescriptor> FixedRegisteredToolSurface { get; }

    Task<McpRuntimeInvocationOutcome> InvokeAsync(
        string pluginAlias,
        string toolName,
        KernelArguments arguments,
        CancellationToken cancellationToken);
}

public sealed class NoOpMcpRuntimeClient : IMcpRuntimeClient
{
    public Task ConnectAsync(McpRuntimeServerDescriptor server, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<string> InvokeAsync(McpRuntimeInvocationRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateUnavailableMessage(request.Server.PluginAlias, request.ToolName));
    }

    private static string CreateUnavailableMessage(string pluginAlias, string toolName)
    {
        return $"MCP tool '{toolName}' via plugin '{pluginAlias}' is currently unavailable. " +
               "This call failed and no non-MCP fallback was executed.";
    }
}

public sealed class SupervisedMcpToolInvoker(IMcpRuntimeSupervisor runtimeSupervisor) : IMcpToolInvoker
{
    public async Task<string> InvokeAsync(
        string pluginAlias,
        string toolName,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        var outcome = await runtimeSupervisor
            .InvokeAsync(pluginAlias, toolName, arguments, cancellationToken)
            .ConfigureAwait(false);

        return outcome.Content;
    }
}

public sealed class McpRuntimeSupervisor : IMcpRuntimeSupervisor, IDisposable
{
    private readonly IReadOnlyDictionary<string, RuntimeSession> _sessionsByAlias;
    private readonly CancellationTokenSource _shutdown = new();

    public McpRuntimeSupervisor(
        IOptions<McpOptions> options,
        McpServerConfigurationResolver resolver,
        IMcpServerPluginAliasProvider aliasProvider,
        IMcpRuntimeClient runtimeClient,
        IMcpReconnectDelayPolicy reconnectDelayPolicy,
        IMcpDelayScheduler delayScheduler,
        IMcpRuntimeTelemetrySink telemetrySink)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(aliasProvider);
        ArgumentNullException.ThrowIfNull(runtimeClient);
        ArgumentNullException.ThrowIfNull(reconnectDelayPolicy);
        ArgumentNullException.ThrowIfNull(delayScheduler);
        ArgumentNullException.ThrowIfNull(telemetrySink);

        var configuredOptions = options.Value ?? new McpOptions();
        var sessions = new Dictionary<string, RuntimeSession>(StringComparer.OrdinalIgnoreCase);

        if (configuredOptions.Enabled)
        {
            var resolution = resolver.Resolve(configuredOptions);
            foreach (var (serverName, resolvedServer) in resolution.ValidServers.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var pluginAlias = aliasProvider.GetPluginAlias(serverName);
                var allowedTools = new HashSet<string>(
                    resolvedServer.AllowedTools
                        .Where(static tool => !string.IsNullOrWhiteSpace(tool))
                        .Select(static tool => tool.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var descriptor = new McpRuntimeServerDescriptor
                {
                    ServerName = serverName,
                    PluginAlias = pluginAlias,
                    Endpoint = resolvedServer.Endpoint,
                    Headers = new Dictionary<string, string>(resolvedServer.Headers, StringComparer.OrdinalIgnoreCase),
                    AllowedTools = allowedTools
                };

                sessions[pluginAlias] = new RuntimeSession(
                    descriptor,
                    runtimeClient,
                    reconnectDelayPolicy,
                    delayScheduler,
                    telemetrySink,
                    _shutdown.Token);
            }
        }

        _sessionsByAlias = sessions;
        FixedRegisteredToolSurface = sessions.Values
            .Select(static session => session.Descriptor)
            .OrderBy(static descriptor => descriptor.PluginAlias, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<McpRuntimeServerDescriptor> FixedRegisteredToolSurface { get; }

    public Task<McpRuntimeInvocationOutcome> InvokeAsync(
        string pluginAlias,
        string toolName,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!_sessionsByAlias.TryGetValue(pluginAlias, out var session))
        {
            return Task.FromResult(FailedUnavailable(pluginAlias, toolName));
        }

        return session.InvokeAsync(toolName, arguments, cancellationToken);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private static McpRuntimeInvocationOutcome FailedUnavailable(string pluginAlias, string toolName)
    {
        return new McpRuntimeInvocationOutcome
        {
            IsSuccess = false,
            Content = CreateMcpUnavailableMessage(pluginAlias, toolName)
        };
    }

    private static string CreateMcpUnavailableMessage(string pluginAlias, string toolName)
    {
        return $"MCP tool '{toolName}' via plugin '{pluginAlias}' is currently unavailable. " +
               "This call failed and no non-MCP fallback was executed.";
    }

    private sealed class RuntimeSession
    {
        private readonly McpRuntimeServerDescriptor _descriptor;
        private readonly IMcpRuntimeClient _runtimeClient;
        private readonly IMcpReconnectDelayPolicy _reconnectDelayPolicy;
        private readonly IMcpDelayScheduler _delayScheduler;
        private readonly IMcpRuntimeTelemetrySink _telemetrySink;
        private readonly CancellationToken _shutdownToken;
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private readonly object _reconnectGate = new();
        private McpRuntimeConnectionState _state;
        private Task? _reconnectTask;

        public RuntimeSession(
            McpRuntimeServerDescriptor descriptor,
            IMcpRuntimeClient runtimeClient,
            IMcpReconnectDelayPolicy reconnectDelayPolicy,
            IMcpDelayScheduler delayScheduler,
            IMcpRuntimeTelemetrySink telemetrySink,
            CancellationToken shutdownToken)
        {
            _descriptor = descriptor;
            _runtimeClient = runtimeClient;
            _reconnectDelayPolicy = reconnectDelayPolicy;
            _delayScheduler = delayScheduler;
            _telemetrySink = telemetrySink;
            _shutdownToken = shutdownToken;
            _state = McpRuntimeConnectionState.Disconnected;
        }

        public McpRuntimeServerDescriptor Descriptor => _descriptor;

        public async Task<McpRuntimeInvocationOutcome> InvokeAsync(
            string toolName,
            KernelArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!_descriptor.AllowedTools.Contains(toolName))
            {
                return FailedUnavailable(_descriptor.PluginAlias, toolName);
            }

            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (_state != McpRuntimeConnectionState.Connected)
            {
                return FailedUnavailable(_descriptor.PluginAlias, toolName);
            }

            var invocationStopwatch = Stopwatch.StartNew();
            try
            {
                var content = await _runtimeClient
                    .InvokeAsync(
                        new McpRuntimeInvocationRequest
                        {
                            Server = _descriptor,
                            ToolName = toolName,
                            Arguments = arguments
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                invocationStopwatch.Stop();
                _telemetrySink.Publish(McpRuntimeTelemetryEvent.InvocationCompleted(
                    _descriptor.ServerName,
                    _descriptor.PluginAlias,
                    toolName,
                    invocationStopwatch.ElapsedMilliseconds,
                    isSuccess: true));

                return new McpRuntimeInvocationOutcome
                {
                    IsSuccess = true,
                    Content = content
                };
            }
            catch (McpRuntimeDisconnectedException disconnectedException)
            {
                invocationStopwatch.Stop();
                _telemetrySink.Publish(McpRuntimeTelemetryEvent.InvocationCompleted(
                    _descriptor.ServerName,
                    _descriptor.PluginAlias,
                    toolName,
                    invocationStopwatch.ElapsedMilliseconds,
                    isSuccess: false,
                    error: McpRuntimeErrorPayload.FromException(disconnectedException)));

                MarkDisconnected(disconnectedException);
                StartReconnectLoopIfNeeded();

                return FailedUnavailable(_descriptor.PluginAlias, toolName);
            }
            catch (Exception exception)
            {
                invocationStopwatch.Stop();
                _telemetrySink.Publish(McpRuntimeTelemetryEvent.InvocationCompleted(
                    _descriptor.ServerName,
                    _descriptor.PluginAlias,
                    toolName,
                    invocationStopwatch.ElapsedMilliseconds,
                    isSuccess: false,
                    error: McpRuntimeErrorPayload.FromException(exception)));

                return FailedUnavailable(_descriptor.PluginAlias, toolName);
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_state == McpRuntimeConnectionState.Connected)
            {
                return;
            }

            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state == McpRuntimeConnectionState.Connected)
                {
                    return;
                }

                _state = McpRuntimeConnectionState.Connecting;
                _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                    McpRuntimeTelemetryEventKind.SessionConnecting,
                    _descriptor.ServerName,
                    _descriptor.PluginAlias,
                    attempt: 1));

                try
                {
                    await _runtimeClient.ConnectAsync(_descriptor, cancellationToken).ConfigureAwait(false);
                    _state = McpRuntimeConnectionState.Connected;

                    _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                        McpRuntimeTelemetryEventKind.SessionConnected,
                        _descriptor.ServerName,
                        _descriptor.PluginAlias,
                        attempt: 1));
                }
                catch (Exception exception)
                {
                    MarkDisconnected(exception);
                    StartReconnectLoopIfNeeded();
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private void MarkDisconnected(Exception exception)
        {
            _state = McpRuntimeConnectionState.Disconnected;
            _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                McpRuntimeTelemetryEventKind.SessionDisconnected,
                _descriptor.ServerName,
                _descriptor.PluginAlias,
                error: McpRuntimeErrorPayload.FromException(exception)));
        }

        private void StartReconnectLoopIfNeeded()
        {
            lock (_reconnectGate)
            {
                if (_reconnectTask is { IsCompleted: false })
                {
                    return;
                }

                _reconnectTask = Task.Run(RunReconnectLoopAsync, _shutdownToken);
            }
        }

        private async Task RunReconnectLoopAsync()
        {
            var attempt = 0;

            while (!_shutdownToken.IsCancellationRequested)
            {
                attempt++;
                var delay = _reconnectDelayPolicy.GetDelay(attempt);
                _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                    McpRuntimeTelemetryEventKind.SessionReconnectScheduled,
                    _descriptor.ServerName,
                    _descriptor.PluginAlias,
                    attempt: attempt,
                    reconnectDelay: delay));

                try
                {
                    await _delayScheduler.DelayAsync(delay, _shutdownToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
                {
                    return;
                }

                await _connectLock.WaitAsync(_shutdownToken).ConfigureAwait(false);
                try
                {
                    if (_state == McpRuntimeConnectionState.Connected)
                    {
                        return;
                    }

                    _state = McpRuntimeConnectionState.Reconnecting;
                    _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                        McpRuntimeTelemetryEventKind.SessionConnecting,
                        _descriptor.ServerName,
                        _descriptor.PluginAlias,
                        attempt: attempt));

                    await _runtimeClient.ConnectAsync(_descriptor, _shutdownToken).ConfigureAwait(false);
                    _state = McpRuntimeConnectionState.Connected;
                    _telemetrySink.Publish(McpRuntimeTelemetryEvent.Lifecycle(
                        McpRuntimeTelemetryEventKind.SessionConnected,
                        _descriptor.ServerName,
                        _descriptor.PluginAlias,
                        attempt: attempt));
                    return;
                }
                catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    MarkDisconnected(exception);
                }
                finally
                {
                    _connectLock.Release();
                }
            }
        }
    }
}
