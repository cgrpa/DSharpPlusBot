namespace TheSexy6BotWorker.Configuration;

public class McpOptions
{
    public const string SectionName = "Mcp";

    public bool Enabled { get; set; } = false;

    public bool StrictStartup { get; set; } = false;

    public Dictionary<string, McpServerOptions> Servers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public class McpServerOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> AllowedTools { get; set; } = [];

    public McpServerStartupOptions Startup { get; set; } = new();
}

public class McpServerStartupOptions
{
    // Placeholders for future startup orchestration behavior.
    public int? ConnectTimeoutSeconds { get; set; }

    public int? InitializeTimeoutSeconds { get; set; }

    public int? ReadyTimeoutSeconds { get; set; }
}
