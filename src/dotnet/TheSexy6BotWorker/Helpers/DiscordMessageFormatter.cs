using DSharpPlus.Entities;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace TheSexy6BotWorker.Helpers
{
    internal static class DiscordMessageFormatter
    {
        private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"];
        private static readonly HttpClient HttpClient = new();

        public static string FormatWithUsername(DiscordMessage message)
        {
            var displayName = message.Author.GlobalName ?? message.Author.Username;
            return $"({displayName})[{message.Author.Username}]: {FormatWithAttachments(message)}";
        }

        public static string FormatWithAttachments(DiscordMessage message)
        {
            var sb = new StringBuilder(message.Content);

            if (message.Attachments.Count > 0)
            {
                sb.AppendLine().AppendLine("[Attachments:]");
                foreach (var a in message.Attachments)
                    sb.AppendLine($"- {a.FileName}: {a.Url}");
            }

            if (message.Embeds.Count > 0)
            {
                sb.AppendLine().AppendLine("[Embeds:]");
                foreach (var embed in message.Embeds)
                {
                    if (!string.IsNullOrEmpty(embed.Title)) sb.AppendLine($"- Title: {embed.Title}");
                    if (!string.IsNullOrEmpty(embed.Description)) sb.AppendLine($"- Description: {embed.Description}");
                    if (embed.Url != null) sb.AppendLine($"- URL: {embed.Url}");
                    if (embed.Image?.Url != null) sb.AppendLine($"- Image: {embed.Image.Url}");
                    if (embed.Thumbnail?.Url != null) sb.AppendLine($"- Thumbnail: {embed.Thumbnail.Url}");
                }
            }

            return sb.ToString();
        }

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1) return $"{(int)duration.TotalSeconds} seconds";
            if (duration.TotalHours < 1) return $"{(int)duration.TotalMinutes} minutes";
            return $"{duration.TotalHours:F1} hours";
        }

        public static async Task AddImagesToHistoryAsync(ChatHistory chatHistory, DiscordMessage message)
        {
            foreach (var attachment in message.Attachments)
            {
                var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
                if (!ImageExtensions.Contains(extension)) continue;

                try
                {
                    chatHistory.AddUserMessage(new ChatMessageContentItemCollection
                    {
                        new ImageContent(new Uri(attachment.Url))
                    });
                }
                catch
                {
                    try
                    {
                        var bytes = await HttpClient.GetByteArrayAsync(attachment.Url);
                        var mimeType = extension switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            ".bmp" => "image/bmp",
                            _ => "image/jpeg"
                        };
                        chatHistory.AddUserMessage(new ChatMessageContentItemCollection
                        {
                            new ImageContent(new Uri($"data:{mimeType};base64,{Convert.ToBase64String(bytes)}"))
                        });
                    }
                    catch { /* skip unloadable image */ }
                }
            }

            foreach (var embed in message.Embeds)
            {
                var imageUrl = embed.Image?.Url?.ToString() ?? embed.Thumbnail?.Url?.ToString();
                if (string.IsNullOrEmpty(imageUrl)) continue;
                try
                {
                    chatHistory.AddUserMessage(new ChatMessageContentItemCollection
                    {
                        new ImageContent(new Uri(imageUrl))
                    });
                }
                catch { /* skip */ }
            }
        }
    }
}
