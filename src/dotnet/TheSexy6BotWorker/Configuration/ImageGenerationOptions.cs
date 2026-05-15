namespace TheSexy6BotWorker.Configuration;

public sealed class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";

    public bool Enabled { get; set; } = true;

    public bool ManualEnabled { get; set; } = true;

    public bool AutoEnabled { get; set; } = true;

    public string? OpenRouterApiKey { get; set; }

    public string OpenRouterEndpoint { get; set; } = "https://openrouter.ai/api/v1/";

    public string DefaultModelId { get; set; } = "black-forest-labs/flux.2-klein-4b";

    public string FallbackModelId { get; set; } = "bytedance-seed/seedream-4.5";

    public string BlobContainerName { get; set; } = "generated-images";

    public string QuotaTableName { get; set; } = "imagequota";

    public string DedupeTableName { get; set; } = "imagededupe";

    public string MetadataTableName { get; set; } = "imagemetadata";

    public string? StorageAccountName { get; set; }

    public string? StorageConnectionString { get; set; }

    public ulong? AllowedGuildId { get; set; }

    public int DailyQuotaLimit { get; set; } = 10;

    public int MaxDecodedBytes { get; set; } = 25 * 1024 * 1024;

    public int OpenRouterTimeoutSeconds { get; set; } = 90;

    public string AspectRatio { get; set; } = "1:1";

    public string ImageSize { get; set; } = "1K";
}
