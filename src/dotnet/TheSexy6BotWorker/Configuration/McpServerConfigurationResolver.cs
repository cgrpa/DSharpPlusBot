using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace TheSexy6BotWorker.Configuration;

public interface IEnvironmentVariableProvider
{
    string? GetEnvironmentVariable(string variableName);
}

public sealed class ProcessEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    public string? GetEnvironmentVariable(string variableName) => Environment.GetEnvironmentVariable(variableName);
}

public enum McpServerSkipReason
{
    MissingInterpolatedValue = 1,
    InvalidDefaultParametersJson = 2,
    MissingAllowedTools = 3,
    ToolDiscoveryFailed = 4
}

public sealed class McpServerSkipDecision
{
    public McpServerSkipDecision(
        string serverName,
        McpServerSkipReason reason,
        string message,
        IReadOnlyList<string>? missingInterpolationKeys = null)
    {
        ServerName = serverName;
        Reason = reason;
        Message = message;
        MissingInterpolationKeys = missingInterpolationKeys ?? [];
    }

    public string ServerName { get; }

    public McpServerSkipReason Reason { get; }

    public string Message { get; }

    public IReadOnlyList<string> MissingInterpolationKeys { get; }
}

public sealed class ResolvedMcpServerOptions
{
    public ResolvedMcpServerOptions(
        string endpoint,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyList<string> allowedTools,
        McpServerStartupOptions startup)
    {
        Endpoint = endpoint;
        Headers = headers;
        AllowedTools = allowedTools;
        Startup = startup;
    }

    public string Endpoint { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public IReadOnlyList<string> AllowedTools { get; }

    public McpServerStartupOptions Startup { get; }
}

public sealed class McpServerConfigurationResolutionResult
{
    public McpServerConfigurationResolutionResult(
        IReadOnlyDictionary<string, ResolvedMcpServerOptions> validServers,
        IReadOnlyList<McpServerSkipDecision> skippedServers)
    {
        ValidServers = validServers;
        SkippedServers = skippedServers;
    }

    public IReadOnlyDictionary<string, ResolvedMcpServerOptions> ValidServers { get; }

    public IReadOnlyList<McpServerSkipDecision> SkippedServers { get; }
}

public sealed partial class McpServerConfigurationResolver
{
    private const string DefaultParametersHeaderName = "DEFAULT_PARAMETERS";
    private readonly IConfiguration _configuration;
    private readonly IEnvironmentVariableProvider _environmentVariableProvider;

    public McpServerConfigurationResolver(
        IConfiguration configuration,
        IEnvironmentVariableProvider? environmentVariableProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environmentVariableProvider = environmentVariableProvider ?? new ProcessEnvironmentVariableProvider();
    }

    public McpServerConfigurationResolutionResult Resolve(McpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var validServers = new Dictionary<string, ResolvedMcpServerOptions>(StringComparer.OrdinalIgnoreCase);
        var skippedServers = new List<McpServerSkipDecision>();

        foreach (var (serverName, serverOptions) in options.Servers.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var resolution = ResolveHeaders(serverOptions.Headers);
            if (resolution.MissingKeys.Count > 0)
            {
                skippedServers.Add(new McpServerSkipDecision(
                    serverName,
                    McpServerSkipReason.MissingInterpolatedValue,
                    $"Skipped server '{serverName}' because one or more interpolated header values were missing.",
                    resolution.MissingKeys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase).ToArray()));
                continue;
            }

            if (resolution.Headers.TryGetValue(DefaultParametersHeaderName, out var defaultParametersValue))
            {
                if (!IsValidJson(defaultParametersValue))
                {
                    skippedServers.Add(new McpServerSkipDecision(
                        serverName,
                        McpServerSkipReason.InvalidDefaultParametersJson,
                        $"Skipped server '{serverName}' because '{DefaultParametersHeaderName}' is not valid JSON."));
                    continue;
                }
            }

            var startupCopy = new McpServerStartupOptions
            {
                ConnectTimeoutSeconds = serverOptions.Startup.ConnectTimeoutSeconds,
                InitializeTimeoutSeconds = serverOptions.Startup.InitializeTimeoutSeconds,
                ReadyTimeoutSeconds = serverOptions.Startup.ReadyTimeoutSeconds
            };

            validServers[serverName] = new ResolvedMcpServerOptions(
                serverOptions.Endpoint,
                new Dictionary<string, string>(resolution.Headers, StringComparer.OrdinalIgnoreCase),
                serverOptions.AllowedTools.ToArray(),
                startupCopy);
        }

        return new McpServerConfigurationResolutionResult(validServers, skippedServers);
    }

    private HeaderResolutionResult ResolveHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (headerName, headerValue) in headers)
        {
            var resolvedValue = HeaderInterpolationPattern().Replace(headerValue, match =>
            {
                var key = match.Groups["key"].Value;
                var configuredValue = _configuration[key];
                if (!string.IsNullOrWhiteSpace(configuredValue))
                {
                    return configuredValue;
                }

                var environmentValue = _environmentVariableProvider.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(environmentValue))
                {
                    return environmentValue;
                }

                missingKeys.Add(key);
                return match.Value;
            });

            resolvedHeaders[headerName] = resolvedValue;
        }

        return new HeaderResolutionResult(resolvedHeaders, missingKeys);
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class HeaderResolutionResult(
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyCollection<string> missingKeys)
    {
        public IReadOnlyDictionary<string, string> Headers { get; } = headers;

        public IReadOnlyCollection<string> MissingKeys { get; } = missingKeys;
    }

    [GeneratedRegex(@"\$\{(?<key>[A-Za-z0-9_]+)\}")]
    private static partial Regex HeaderInterpolationPattern();
}
