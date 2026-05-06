using DSharpPlus.EventArgs;

namespace TheSexy6BotWorker.Helpers
{
    internal static class DiscordMessageSender
    {
        private const int MaxLength = 1980;

        public static async Task SendChunkedAsync(MessageCreatedEventArgs e, string content)
        {
            if (content.Length <= MaxLength)
            {
                await e.Message.RespondAsync(content);
                return;
            }

            var remaining = content;
            var first = true;

            while (remaining.Length > 0)
            {
                string chunk;
                bool isLast;

                if (remaining.Length <= MaxLength)
                {
                    chunk = remaining;
                    remaining = string.Empty;
                    isLast = true;
                }
                else
                {
                    int cutAt = remaining.LastIndexOf(' ', MaxLength);
                    if (cutAt <= 0) cutAt = MaxLength;
                    chunk = remaining[..cutAt];
                    remaining = remaining[cutAt..].TrimStart();
                    isLast = remaining.Length == 0;
                }

                var prefix = first ? "" : "⤴️ ";
                var suffix = isLast ? "" : " ⤵️";
                await e.Message.RespondAsync($"{prefix}{chunk}{suffix}");
                first = false;
            }
        }
    }
}
