namespace TheSexy6BotWorker.Configuration;

public sealed class TavilyApiOptions
{
    public const string SectionName = "TavilyApi";
    public const string DefaultEndpoint = "https://api.tavily.com";

    public string Endpoint { get; set; } = DefaultEndpoint;

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 1;

    public int BaseDelayMilliseconds { get; set; } = 150;

    public int MaxDelayMilliseconds { get; set; } = 1000;
}
