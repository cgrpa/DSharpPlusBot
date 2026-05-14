namespace TheSexy6BotWorker.Configuration;

public sealed class TavilyApiOptions
{
    public const string SectionName = "TavilyApi";
    public const string DefaultEndpoint = "https://api.tavily.com";

    public string Endpoint { get; set; } = DefaultEndpoint;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 2;

    public int BaseDelayMilliseconds { get; set; } = 250;

    public int MaxDelayMilliseconds { get; set; } = 4000;
}
