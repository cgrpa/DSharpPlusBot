namespace TheSexy6BotWorker.Models;

public sealed record ImageGenerationResult
{
    public bool Success { get; init; }

    public bool IsNewGeneration { get; init; }

    public bool UsedFallback { get; init; }

    public string Prompt { get; init; } = string.Empty;

    public string PromptHash { get; init; } = string.Empty;

    public ImageGenerationModelChoice RequestedModel { get; init; } = ImageGenerationModelChoice.Flux;

    public ImageGenerationModelChoice ResolvedModel { get; init; } = ImageGenerationModelChoice.Flux;

    public string ModelId { get; init; } = string.Empty;

    public string BlobName { get; init; } = string.Empty;

    public string BlobUrl { get; init; } = string.Empty;

    public string ContentType { get; init; } = "image/png";

    public byte[]? ImageBytes { get; init; }

    public string DisplayMessage { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public GeneratedImageHistoryMarker? HistoryMarker { get; init; }

    public string ResponseText => Success
        ? DisplayMessage
        : ErrorMessage ?? DisplayMessage;

    public static ImageGenerationResult CreateSuccess(
        string prompt,
        string promptHash,
        ImageGenerationModelChoice requestedModel,
        ImageGenerationModelChoice resolvedModel,
        string modelId,
        string blobName,
        string blobUrl,
        string contentType,
        DateTimeOffset createdAtUtc,
        string displayMessage,
        bool usedFallback,
        GeneratedImageHistoryMarker? historyMarker = null,
        byte[]? imageBytes = null,
        bool isNewGeneration = true)
    {
        return new ImageGenerationResult
        {
            Success = true,
            IsNewGeneration = isNewGeneration,
            UsedFallback = usedFallback,
            Prompt = prompt,
            PromptHash = promptHash,
            RequestedModel = requestedModel,
            ResolvedModel = resolvedModel,
            ModelId = modelId,
            BlobName = blobName,
            BlobUrl = blobUrl,
            ContentType = contentType,
            ImageBytes = imageBytes,
            DisplayMessage = displayMessage,
            CreatedAtUtc = createdAtUtc,
            HistoryMarker = historyMarker
        };
    }

    public static ImageGenerationResult CreateFailure(
        string prompt,
        string promptHash,
        ImageGenerationModelChoice requestedModel,
        string errorMessage,
        bool isNewGeneration,
        DateTimeOffset createdAtUtc,
        GeneratedImageHistoryMarker? historyMarker = null)
    {
        return new ImageGenerationResult
        {
            Success = false,
            IsNewGeneration = isNewGeneration,
            Prompt = prompt,
            PromptHash = promptHash,
            RequestedModel = requestedModel,
            ResolvedModel = requestedModel,
            ErrorMessage = errorMessage,
            DisplayMessage = errorMessage,
            CreatedAtUtc = createdAtUtc,
            HistoryMarker = historyMarker
        };
    }
}
