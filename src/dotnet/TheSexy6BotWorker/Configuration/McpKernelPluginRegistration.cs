using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace TheSexy6BotWorker.Configuration;

public enum McpTransportKind
{
    StreamableHttp = 1,
    ServerSentEvents = 2
}

public sealed class McpToolDescriptor
{
    public McpToolDescriptor(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string? Description { get; }
}

public sealed class McpServerToolDiscoveryRequest
{
    public required string ServerName { get; init; }

    public required string Endpoint { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public required McpTransportKind TransportKind { get; init; }
}

public sealed class McpServerToolDiscoveryResult
{
    private McpServerToolDiscoveryResult(bool isSuccess, IReadOnlyList<McpToolDescriptor> tools, string? message)
    {
        IsSuccess = isSuccess;
        Tools = tools;
        Message = message;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<McpToolDescriptor> Tools { get; }

    public string? Message { get; }

    public static McpServerToolDiscoveryResult Success(IReadOnlyList<McpToolDescriptor> tools) =>
        new(true, tools, null);

    public static McpServerToolDiscoveryResult Failure(string? message = null) =>
        new(false, [], message);
}

public interface IMcpServerToolDiscoveryClient
{
    McpTransportKind TransportKind { get; }

    Task<McpServerToolDiscoveryResult> DiscoverToolsAsync(
        McpServerToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IMcpToolInvoker
{
    Task<string> InvokeAsync(
        string pluginAlias,
        string toolName,
        KernelArguments arguments,
        CancellationToken cancellationToken);
}

public sealed class UnavailableMcpToolInvoker : IMcpToolInvoker
{
    public Task<string> InvokeAsync(
        string pluginAlias,
        string toolName,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            $"MCP tool '{toolName}' via plugin '{pluginAlias}' is not available in this rollout stage.");
    }
}

public interface IMcpKernelPluginRegistrar
{
    void RegisterAllowedTools(
        IKernelBuilderPlugins plugins,
        string pluginAlias,
        string serverName,
        IReadOnlyList<McpToolDescriptor> allowedTools);
}

public sealed class SemanticKernelMcpPluginRegistrar(IMcpToolInvoker toolInvoker) : IMcpKernelPluginRegistrar
{
    public void RegisterAllowedTools(
        IKernelBuilderPlugins plugins,
        string pluginAlias,
        string serverName,
        IReadOnlyList<McpToolDescriptor> allowedTools)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(allowedTools);

        var functions = new List<KernelFunction>(allowedTools.Count);
        foreach (var tool in allowedTools)
        {
            var toolName = tool.Name;
            var description = string.IsNullOrWhiteSpace(tool.Description)
                ? $"Invokes remote MCP tool '{toolName}' from server '{serverName}'."
                : tool.Description;

            functions.Add(KernelFunctionFactory.CreateFromMethod(
                method: (KernelArguments arguments, CancellationToken cancellationToken) =>
                    toolInvoker.InvokeAsync(pluginAlias, toolName, arguments, cancellationToken),
                functionName: toolName,
                description: description));
        }

        plugins.AddFromFunctions(pluginAlias, functions);
    }
}

public interface IMcpServerPluginAliasProvider
{
    string GetPluginAlias(string serverName);
}

public sealed partial class StableMcpServerPluginAliasProvider : IMcpServerPluginAliasProvider
{
    private const string TavilyServerName = "Tavily";
    private const string TavilyPluginAlias = "TavilyRemoteMcp";

    public string GetPluginAlias(string serverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        if (string.Equals(serverName, TavilyServerName, StringComparison.OrdinalIgnoreCase))
        {
            return TavilyPluginAlias;
        }

        var normalized = InvalidAliasCharsRegex().Replace(serverName, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "McpServer";
        }

        return $"{normalized}RemoteMcp";
    }

    [GeneratedRegex("[^0-9A-Za-z_]", RegexOptions.Compiled)]
    private static partial Regex InvalidAliasCharsRegex();
}

public sealed class McpServerPluginRegistrationDecision
{
    public McpServerPluginRegistrationDecision(
        string serverName,
        string pluginAlias,
        string transport,
        IReadOnlyList<string> registeredTools)
    {
        ServerName = serverName;
        PluginAlias = pluginAlias;
        Transport = transport;
        RegisteredTools = registeredTools;
    }

    public string ServerName { get; }

    public string PluginAlias { get; }

    public string Transport { get; }

    public IReadOnlyList<string> RegisteredTools { get; }
}

public sealed class McpKernelPluginRegistrationResult
{
    public McpKernelPluginRegistrationResult(
        IReadOnlyList<McpServerPluginRegistrationDecision> registeredServers,
        IReadOnlyList<McpServerSkipDecision> skippedServers)
    {
        RegisteredServers = registeredServers;
        SkippedServers = skippedServers;
    }

    public IReadOnlyList<McpServerPluginRegistrationDecision> RegisteredServers { get; }

    public IReadOnlyList<McpServerSkipDecision> SkippedServers { get; }
}

public sealed class McpStrictStartupException : Exception
{
    public McpStrictStartupException(McpKernelPluginRegistrationResult registrationResult)
        : base(CreateMessage(registrationResult))
    {
        RegistrationResult = registrationResult ?? throw new ArgumentNullException(nameof(registrationResult));
    }

    public McpKernelPluginRegistrationResult RegistrationResult { get; }

    private static string CreateMessage(McpKernelPluginRegistrationResult registrationResult)
    {
        var skippedServers = registrationResult.SkippedServers
            .Select(static s => s.ServerName)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return $"MCP strict startup failed because {registrationResult.SkippedServers.Count} server(s) were skipped: {string.Join(", ", skippedServers)}.";
    }
}

public sealed class McpKernelPluginRegistrationCoordinator
{
    internal const int DefaultServerStartupTimeoutSeconds = 10;

    private readonly McpServerConfigurationResolver _resolver;
    private readonly IMcpServerPluginAliasProvider _aliasProvider;
    private readonly IMcpKernelPluginRegistrar _pluginRegistrar;
    private readonly IReadOnlyList<IMcpServerToolDiscoveryClient> _discoveryClients;
    private readonly TimeSpan _defaultServerStartupTimeout;

    public McpKernelPluginRegistrationCoordinator(
        McpServerConfigurationResolver resolver,
        IEnumerable<IMcpServerToolDiscoveryClient> discoveryClients,
        IMcpServerPluginAliasProvider? aliasProvider = null,
        IMcpKernelPluginRegistrar? pluginRegistrar = null,
        TimeSpan? defaultServerStartupTimeout = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _aliasProvider = aliasProvider ?? new StableMcpServerPluginAliasProvider();
        _pluginRegistrar = pluginRegistrar ?? new SemanticKernelMcpPluginRegistrar(new UnavailableMcpToolInvoker());
        _discoveryClients = discoveryClients?
            .OrderBy(static c => c.TransportKind)
            .ToArray() ?? throw new ArgumentNullException(nameof(discoveryClients));
        _defaultServerStartupTimeout = defaultServerStartupTimeout ?? TimeSpan.FromSeconds(DefaultServerStartupTimeoutSeconds);
    }

    public async Task<McpKernelPluginRegistrationResult> RegisterAsync(
        IKernelBuilderPlugins plugins,
        McpOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return new McpKernelPluginRegistrationResult([], []);
        }

        var resolution = _resolver.Resolve(options);
        var skippedServers = new List<McpServerSkipDecision>(resolution.SkippedServers);
        var registeredServers = new List<McpServerPluginRegistrationDecision>();
        var serverRegistrationTasks = resolution.ValidServers
            .OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => (pair.Key, pair.Value))
            .Select(pair => EvaluateServerRegistrationAsync(pair.Key, pair.Value, cancellationToken))
            .ToArray();

        var registrationEvaluations = await Task.WhenAll(serverRegistrationTasks).ConfigureAwait(false);
        foreach (var evaluation in registrationEvaluations
                     .OrderBy(static r => r.ServerName, StringComparer.OrdinalIgnoreCase))
        {
            if (evaluation.SkipDecision is not null)
            {
                skippedServers.Add(evaluation.SkipDecision);
                continue;
            }

            var registration = evaluation.SuccessfulRegistration!;
            _pluginRegistrar.RegisterAllowedTools(
                plugins,
                registration.PluginAlias,
                registration.ServerName,
                registration.SelectedTools);
            registeredServers.Add(new McpServerPluginRegistrationDecision(
                registration.ServerName,
                registration.PluginAlias,
                registration.Transport,
                registration.SelectedTools.Select(static t => t.Name).ToArray()));
        }

        var registrationResult = new McpKernelPluginRegistrationResult(registeredServers, skippedServers);
        if (options.StrictStartup && registrationResult.SkippedServers.Count > 0)
        {
            throw new McpStrictStartupException(registrationResult);
        }

        return registrationResult;
    }

    private async Task<ServerRegistrationEvaluation> EvaluateServerRegistrationAsync(
        string serverName,
        ResolvedMcpServerOptions serverOptions,
        CancellationToken cancellationToken)
    {
        var pluginAlias = _aliasProvider.GetPluginAlias(serverName);
        var timeout = ResolveStartupTimeout(serverOptions.Startup);
        var discovery = await DiscoverToolsAsync(serverName, serverOptions, timeout, cancellationToken).ConfigureAwait(false);
        if (discovery.IsTimedOut)
        {
            return new ServerRegistrationEvaluation(
                serverName,
                new McpServerSkipDecision(
                    serverName,
                    McpServerSkipReason.StartupTimeout,
                    $"Skipped server '{serverName}' because startup exceeded the timeout budget of {timeout.TotalSeconds:0} second(s)."),
                null);
        }

        if (discovery.FailureWithoutTimeout)
        {
            return new ServerRegistrationEvaluation(
                serverName,
                new McpServerSkipDecision(
                    serverName,
                    McpServerSkipReason.ToolDiscoveryFailed,
                    $"Skipped server '{serverName}' because no transport successfully discovered tools."),
                null);
        }

        var selectedDiscovery = discovery.SuccessfulDiscovery!.Value;
        var discoveredTools = new HashSet<string>(
            selectedDiscovery.Result.Tools.Select(static t => t.Name),
            StringComparer.OrdinalIgnoreCase);
        var requestedTools = serverOptions.AllowedTools
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingAllowedTools = requestedTools
            .Where(tool => !discoveredTools.Contains(tool))
            .OrderBy(static t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingAllowedTools.Length > 0)
        {
            return new ServerRegistrationEvaluation(
                serverName,
                new McpServerSkipDecision(
                    serverName,
                    McpServerSkipReason.MissingAllowedTools,
                    $"Skipped server '{serverName}' because one or more allowed tools were missing from discovery.",
                    missingAllowedTools),
                null);
        }

        var selectedTools = selectedDiscovery.Result.Tools
            .Where(t => requestedTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(static t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ServerRegistrationEvaluation(
            serverName,
            null,
            new SuccessfulServerRegistration(
                serverName,
                pluginAlias,
                selectedDiscovery.Client.TransportKind.ToString(),
                selectedTools));
    }

    private async Task<ServerDiscoveryAttemptResult> DiscoverToolsAsync(
        string serverName,
        ResolvedMcpServerOptions serverOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        foreach (var discoveryClient in _discoveryClients)
        {
            var request = new McpServerToolDiscoveryRequest
            {
                ServerName = serverName,
                Endpoint = serverOptions.Endpoint,
                Headers = serverOptions.Headers,
                TransportKind = discoveryClient.TransportKind
            };

            McpServerToolDiscoveryResult result;
            try
            {
                result = await discoveryClient.DiscoverToolsAsync(request, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsTimeout(timeoutCts.Token, cancellationToken))
            {
                return ServerDiscoveryAttemptResult.TimedOut();
            }
            catch (Exception)
            {
                // Transport-specific failures should not block trying fallback transport kinds.
                continue;
            }

            if (result.IsSuccess)
            {
                return ServerDiscoveryAttemptResult.Succeeded(discoveryClient, result);
            }

            if (IsTimeout(timeoutCts.Token, cancellationToken))
            {
                return ServerDiscoveryAttemptResult.TimedOut();
            }
        }

        if (IsTimeout(timeoutCts.Token, cancellationToken))
        {
            return ServerDiscoveryAttemptResult.TimedOut();
        }

        return ServerDiscoveryAttemptResult.FailedWithoutTimeout();
    }

    private static bool IsTimeout(CancellationToken timeoutToken, CancellationToken rootToken) =>
        timeoutToken.IsCancellationRequested && !rootToken.IsCancellationRequested;

    private TimeSpan ResolveStartupTimeout(McpServerStartupOptions startup)
    {
        var configuredTimeoutSeconds = new[]
        {
            startup.ConnectTimeoutSeconds,
            startup.InitializeTimeoutSeconds,
            startup.ReadyTimeoutSeconds
        }
        .Where(static seconds => seconds is > 0)
        .Select(static seconds => seconds!.Value)
        .DefaultIfEmpty((int)_defaultServerStartupTimeout.TotalSeconds)
        .Min();

        return TimeSpan.FromSeconds(configuredTimeoutSeconds);
    }

    private sealed record SuccessfulServerRegistration(
        string ServerName,
        string PluginAlias,
        string Transport,
        IReadOnlyList<McpToolDescriptor> SelectedTools);

    private sealed record ServerRegistrationEvaluation(
        string ServerName,
        McpServerSkipDecision? SkipDecision,
        SuccessfulServerRegistration? SuccessfulRegistration);

    private sealed class ServerDiscoveryAttemptResult
    {
        private ServerDiscoveryAttemptResult(
            bool isTimedOut,
            bool failureWithoutTimeout,
            (IMcpServerToolDiscoveryClient Client, McpServerToolDiscoveryResult Result)? successfulDiscovery)
        {
            IsTimedOut = isTimedOut;
            FailureWithoutTimeout = failureWithoutTimeout;
            SuccessfulDiscovery = successfulDiscovery;
        }

        public bool IsTimedOut { get; }

        public bool FailureWithoutTimeout { get; }

        public (IMcpServerToolDiscoveryClient Client, McpServerToolDiscoveryResult Result)? SuccessfulDiscovery { get; }

        public static ServerDiscoveryAttemptResult TimedOut() => new(true, false, null);

        public static ServerDiscoveryAttemptResult FailedWithoutTimeout() => new(false, true, null);

        public static ServerDiscoveryAttemptResult Succeeded(
            IMcpServerToolDiscoveryClient client,
            McpServerToolDiscoveryResult result) => new(false, false, (client, result));
    }
}

public sealed class NoOpStreamableHttpMcpToolDiscoveryClient : IMcpServerToolDiscoveryClient
{
    public McpTransportKind TransportKind => McpTransportKind.StreamableHttp;

    public Task<McpServerToolDiscoveryResult> DiscoverToolsAsync(
        McpServerToolDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(McpServerToolDiscoveryResult.Failure("Not implemented."));
    }
}

public sealed class NoOpSseMcpToolDiscoveryClient : IMcpServerToolDiscoveryClient
{
    public McpTransportKind TransportKind => McpTransportKind.ServerSentEvents;

    public Task<McpServerToolDiscoveryResult> DiscoverToolsAsync(
        McpServerToolDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(McpServerToolDiscoveryResult.Failure("Not implemented."));
    }
}
