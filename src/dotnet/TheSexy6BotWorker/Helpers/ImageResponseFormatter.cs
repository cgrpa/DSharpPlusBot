using DSharpPlus.Entities;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Helpers;

public static class ImageResponseFormatter
{
    public static string BuildContent(ImageGenerationResult result, string? narrative = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var content = string.IsNullOrWhiteSpace(narrative)
            ? result.ResponseText
            : narrative.Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            content = result.Success
                ? "Image generated."
                : result.ResponseText;
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.BlobUrl))
        {
            return content;
        }

        if (content.Contains(result.BlobUrl, StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        return $"{content}{Environment.NewLine}{result.BlobUrl}";
    }

    public static DiscordEmbed BuildEmbed(ImageGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new DiscordEmbedBuilder()
            .WithTitle(result.UsedFallback ? "Image generated with fallback" : "Image generated")
            .WithDescription(BuildDescription(result))
            .WithImageUrl(result.BlobUrl)
            .WithColor(result.UsedFallback ? DiscordColor.Orange : DiscordColor.Blurple);

        if (!string.IsNullOrWhiteSpace(result.BlobName))
        {
            builder.WithFooter($"Blob: {result.BlobName}");
        }

        return builder.Build();
    }

    private static string BuildDescription(ImageGenerationResult result)
    {
        var description = new List<string>
        {
            $"Model: `{result.ResolvedModel.ToAlias()}`",
            $"Created: `{result.CreatedAtUtc:O}`"
        };

        if (result.UsedFallback)
        {
            description.Add("Fallback: `seedream`");
        }

        return string.Join(Environment.NewLine, description);
    }
}
