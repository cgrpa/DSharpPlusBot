namespace TheSexy6BotWorker.Configuration;

public enum RuntimeMode
{
    Development,
    Remote
}

public static class HostEnvironmentMode
{
    public const string DevelopmentCommandPrefix = "test-";

    public static RuntimeMode GetRuntimeMode(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        return hostEnvironment.IsDevelopment() ? RuntimeMode.Development : RuntimeMode.Remote;
    }

    public static bool ShouldLoadUserSecrets(IHostEnvironment hostEnvironment)
    {
        return GetRuntimeMode(hostEnvironment) == RuntimeMode.Development;
    }

    public static string GetMessagePrefix(IHostEnvironment hostEnvironment)
    {
        return GetRuntimeMode(hostEnvironment) == RuntimeMode.Development ? DevelopmentCommandPrefix : string.Empty;
    }

    [Obsolete("Use GetMessagePrefix instead.")]
    public static string GetBotCommandPrefix(IHostEnvironment hostEnvironment)
    {
        return GetMessagePrefix(hostEnvironment);
    }
}
