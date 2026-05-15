using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace TheSexy6BotWorker.Helpers
{
    internal static class DiscordMessageSender
    {
        private const int MaxLength = 1980;

        public static async Task SendChunkedAsync(
            MessageCreatedEventArgs e,
            string content,
            DiscordEmbed? embed = null,
            byte[]? imageBytes = null,
            string? imageFileName = null)
        {
            if (content.Length <= MaxLength)
            {
                if (imageBytes is not null && imageBytes.Length > 0)
                {
                    var fileName = string.IsNullOrWhiteSpace(imageFileName) ? "generated-image.png" : imageFileName;
                    await using var stream = new MemoryStream(imageBytes, writable: false);
                    var builder = new DiscordMessageBuilder()
                        .WithContent(content)
                        .AddFile(fileName, stream);

                    if (embed is not null)
                    {
                        builder.AddEmbed(embed);
                    }

                    await e.Message.RespondAsync(builder);
                }
                else
                {
                    if (embed is null)
                    {
                        await e.Message.RespondAsync(content);
                    }
                    else
                    {
                        await e.Message.RespondAsync(content, embed);
                    }
                }

                return;
            }

            var remaining = content;
            var first = true;

            while (remaining.Length > 0)
            {
                string chunk;
                var isLast = false;

                if (remaining.Length <= MaxLength)
                {
                    chunk = remaining;
                    remaining = string.Empty;
                    isLast = true;
                }
                else
                {
                    var cutAt = remaining.LastIndexOf(' ', MaxLength);
                    if (cutAt <= 0) cutAt = MaxLength;
                    chunk = remaining[..cutAt];
                    remaining = remaining[cutAt..].TrimStart();
                    isLast = remaining.Length == 0;
                }

                var prefix = first ? string.Empty : "⤴️ ";
                var suffix = isLast ? string.Empty : " ⤵️";
                var chunkContent = $"{prefix}{chunk}{suffix}";

                if (first && imageBytes is not null && imageBytes.Length > 0)
                {
                    var fileName = string.IsNullOrWhiteSpace(imageFileName) ? "generated-image.png" : imageFileName;
                    await using var stream = new MemoryStream(imageBytes, writable: false);
                    var builder = new DiscordMessageBuilder()
                        .WithContent(chunkContent)
                        .AddFile(fileName, stream);

                    if (embed is not null)
                    {
                        builder.AddEmbed(embed);
                    }

                    await e.Message.RespondAsync(builder);
                }
                else if (first && embed is not null)
                {
                    await e.Message.RespondAsync(chunkContent, embed);
                }
                else
                {
                    await e.Message.RespondAsync(chunkContent);
                }

                first = false;
            }
        }
    }
}
