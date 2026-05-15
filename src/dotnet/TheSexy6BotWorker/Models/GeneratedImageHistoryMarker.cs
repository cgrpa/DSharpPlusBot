using System.Text.Json;
using System.Text.Json.Serialization;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Models;

public sealed record GeneratedImageHistoryMarker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public const int CurrentVersion = 1;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "generated_image";

    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    [JsonPropertyName("sourceMessageId")]
    public ulong SourceMessageId { get; init; }

    [JsonPropertyName("channelId")]
    public ulong ChannelId { get; init; }

    [JsonPropertyName("userId")]
    public ulong UserId { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = "manual";

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("promptHash")]
    public string PromptHash { get; init; } = string.Empty;

    [JsonPropertyName("requestedModel")]
    public string RequestedModel { get; init; } = string.Empty;

    [JsonPropertyName("resolvedModel")]
    public string ResolvedModel { get; init; } = string.Empty;

    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = string.Empty;

    [JsonPropertyName("usedFallback")]
    public bool UsedFallback { get; init; }

    [JsonPropertyName("blobName")]
    public string BlobName { get; init; } = string.Empty;

    [JsonPropertyName("blobUrl")]
    public string BlobUrl { get; init; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static bool TryParse(string json, out GeneratedImageHistoryMarker? marker)
    {
        marker = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<GeneratedImageHistoryMarker>(json, SerializerOptions);
            if (parsed is null || parsed.Version != CurrentVersion || !string.Equals(parsed.Kind, "generated_image", StringComparison.Ordinal))
            {
                return false;
            }

            marker = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static GeneratedImageHistoryMarker FromResult(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return new GeneratedImageHistoryMarker
        {
            SourceMessageId = context.SourceMessageId,
            ChannelId = context.ChannelId,
            UserId = context.UserId,
            Origin = context.IsAuto ? "auto" : "manual",
            Prompt = result.Prompt,
            PromptHash = result.PromptHash,
            RequestedModel = result.RequestedModel.ToAlias(),
            ResolvedModel = result.ResolvedModel.ToAlias(),
            ModelId = result.ModelId,
            UsedFallback = result.UsedFallback,
            BlobName = result.BlobName,
            BlobUrl = result.BlobUrl,
            ContentType = result.ContentType,
            CreatedAtUtc = result.CreatedAtUtc
        };
    }
}
