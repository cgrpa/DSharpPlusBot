using System.Text;
using Microsoft.Extensions.Logging;

namespace TheSexy6BotWorker.Services;

public enum McpRuntimeTelemetryEventKind
{
    SessionConnecting = 1,
    SessionConnected = 2,
    SessionDisconnected = 3,
    SessionReconnectScheduled = 4,
    InvocationCompleted = 5
}

public sealed class McpRuntimeErrorPayload
{
    public required string Category { get; init; }

    public required string Message { get; init; }

    public static McpRuntimeErrorPayload FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new McpRuntimeErrorPayload
        {
            Category = exception.GetType().Name,
            Message = Sanitize(exception.Message)
        };
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '\r' => ' ',
                '\n' => ' ',
                _ => c
            });
        }

        var oneLine = builder.ToString().Trim();
        if (oneLine.Length <= 240)
        {
            return oneLine;
        }

        return $"{oneLine[..240]}...";
    }
}

public sealed class McpRuntimeTelemetryEvent
{
    public required McpRuntimeTelemetryEventKind Kind { get; init; }

    public required string ServerName { get; init; }

    public required string PluginAlias { get; init; }

    public string? ToolName { get; init; }

    public long? LatencyMs { get; init; }

    public bool? IsSuccess { get; init; }

    public int? Attempt { get; init; }

    public long? ReconnectDelayMs { get; init; }

    public McpRuntimeErrorPayload? Error { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static McpRuntimeTelemetryEvent Lifecycle(
        McpRuntimeTelemetryEventKind kind,
        string serverName,
        string pluginAlias,
        int? attempt = null,
        TimeSpan? reconnectDelay = null,
        McpRuntimeErrorPayload? error = null)
    {
        return new McpRuntimeTelemetryEvent
        {
            Kind = kind,
            ServerName = serverName,
            PluginAlias = pluginAlias,
            Attempt = attempt,
            ReconnectDelayMs = reconnectDelay.HasValue
                ? Convert.ToInt64(Math.Round(reconnectDelay.Value.TotalMilliseconds, MidpointRounding.AwayFromZero))
                : null,
            Error = error
        };
    }

    public static McpRuntimeTelemetryEvent InvocationCompleted(
        string serverName,
        string pluginAlias,
        string toolName,
        long latencyMs,
        bool isSuccess,
        McpRuntimeErrorPayload? error = null)
    {
        return new McpRuntimeTelemetryEvent
        {
            Kind = McpRuntimeTelemetryEventKind.InvocationCompleted,
            ServerName = serverName,
            PluginAlias = pluginAlias,
            ToolName = toolName,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            Error = error
        };
    }
}

public interface IMcpRuntimeTelemetrySink
{
    void Publish(McpRuntimeTelemetryEvent telemetryEvent);
}

public sealed class LoggerMcpRuntimeTelemetrySink(ILogger<LoggerMcpRuntimeTelemetrySink> logger) : IMcpRuntimeTelemetrySink
{
    public void Publish(McpRuntimeTelemetryEvent telemetryEvent)
    {
        ArgumentNullException.ThrowIfNull(telemetryEvent);

        var level = telemetryEvent.Kind == McpRuntimeTelemetryEventKind.InvocationCompleted && telemetryEvent.IsSuccess == true
            ? LogLevel.Information
            : telemetryEvent.Error is null ? LogLevel.Information : LogLevel.Warning;

        logger.Log(level, "MCP runtime telemetry event {@McpTelemetry}", telemetryEvent);
    }
}
