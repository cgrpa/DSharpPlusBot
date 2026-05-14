namespace TheSexy6BotWorker.Models;

public sealed record ImageGenerationRequest
{
    public string Prompt { get; init; } = string.Empty;

    public ImageGenerationModelChoice? RequestedModel { get; init; }

    public string? AspectRatio { get; init; }

    public string? ImageSize { get; init; }
}
