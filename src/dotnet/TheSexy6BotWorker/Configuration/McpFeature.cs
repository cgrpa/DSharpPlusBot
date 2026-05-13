using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Configuration;

public interface IMcpFeature
{
    Task RegisterKernelPluginsAsync(
        IKernelBuilderPlugins plugins,
        CancellationToken cancellationToken);
}

public sealed class McpFeature : IMcpFeature
{
    private readonly McpOptions _options;
    private readonly McpKernelPluginRegistrationCoordinator _registrationCoordinator;
    private readonly IMcpRuntimeSupervisor _runtimeSupervisor;
    private readonly ILogger<McpFeature> _logger;

    public McpFeature(
        IOptions<McpOptions> options,
        McpKernelPluginRegistrationCoordinator registrationCoordinator,
        IMcpRuntimeSupervisor runtimeSupervisor,
        ILogger<McpFeature> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _registrationCoordinator = registrationCoordinator ?? throw new ArgumentNullException(nameof(registrationCoordinator));
        _runtimeSupervisor = runtimeSupervisor ?? throw new ArgumentNullException(nameof(runtimeSupervisor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RegisterKernelPluginsAsync(
        IKernelBuilderPlugins plugins,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        try
        {
            var registration = await _registrationCoordinator
                .RegisterAsync(plugins, _options, cancellationToken)
                .ConfigureAwait(false);
            LogStartupSummary(registration);
        }
        catch (McpStrictStartupException ex)
        {
            LogStartupSummary(ex.RegistrationResult);
            throw;
        }
    }

    private void LogStartupSummary(McpKernelPluginRegistrationResult registrationResult)
    {
        var registeredServerCount = registrationResult.RegisteredServers.Count;
        var skippedServerCount = registrationResult.SkippedServers.Count;
        var registeredToolCount = registrationResult.RegisteredServers
            .Sum(static server => server.RegisteredTools.Count);
        var runtimeServerCount = _runtimeSupervisor.FixedRegisteredToolSurface.Count;

        _logger.LogInformation(
            "MCP startup summary: registered servers={RegisteredServerCount}, registered tools={RegisteredToolCount}, skipped servers={SkippedServerCount}, strict startup={StrictStartup}, fixed runtime servers={RuntimeServerCount}.",
            registeredServerCount,
            registeredToolCount,
            skippedServerCount,
            _options.StrictStartup,
            runtimeServerCount);

        foreach (var registration in registrationResult.RegisteredServers
                     .OrderBy(static r => r.ServerName, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "MCP startup registered server {ServerName} as plugin {PluginAlias} via {Transport} with tools: {Tools}.",
                registration.ServerName,
                registration.PluginAlias,
                registration.Transport,
                string.Join(", ", registration.RegisteredTools));
        }

        foreach (var skipped in registrationResult.SkippedServers
                     .OrderBy(static s => s.ServerName, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "MCP startup skipped server {ServerName}: {Reason} ({SanitizedReason}). Detail: {DetailMessage}",
                skipped.ServerName,
                skipped.Reason,
                ToSanitizedSkipReason(skipped),
                skipped.Message);
        }
    }

    private static string ToSanitizedSkipReason(McpServerSkipDecision skipped)
    {
        return skipped.Reason switch
        {
            McpServerSkipReason.MissingInterpolatedValue =>
                FormatSanitizedListReason(
                    "missing interpolated keys",
                    skipped.MissingInterpolationKeys),
            McpServerSkipReason.MissingAllowedTools =>
                FormatSanitizedListReason(
                    "missing allowed tools",
                    skipped.MissingInterpolationKeys),
            McpServerSkipReason.InvalidDefaultParametersJson =>
                "DEFAULT_PARAMETERS was invalid JSON.",
            McpServerSkipReason.ToolDiscoveryFailed =>
                "No configured transport completed tool discovery successfully.",
            McpServerSkipReason.StartupTimeout =>
                "Startup timeout budget was exceeded.",
            _ =>
                "Server was skipped by startup policy."
        };
    }

    private static string FormatSanitizedListReason(
        string prefix,
        IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return prefix;
        }

        return $"{prefix}: {string.Join(", ", values.OrderBy(static v => v, StringComparer.OrdinalIgnoreCase))}.";
    }
}
