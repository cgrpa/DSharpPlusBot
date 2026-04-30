using DSharpPlus.Entities;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Helpers
{
    internal static class DiscordReplyChainHelper
    {
        public static async Task AddToHistoryAsync(ChatHistory chatHistory, DiscordMessage message, IBotConfiguration bot)
        {
            var chain = await GetChainAsync(message);
            chain.Reverse();

            foreach (var msg in chain)
            {
                if (msg.Author.IsBot)
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
                else
                {
                    chatHistory.AddUserMessage(DiscordMessageFormatter.FormatWithUsername(msg));
                    if (bot.SupportsImages)
                        await DiscordMessageFormatter.AddImagesToHistoryAsync(chatHistory, msg);
                }
            }
        }

        private static async Task<List<DiscordMessage>> GetChainAsync(DiscordMessage leaf, int maxDepth = 10)
        {
            var chain = new List<DiscordMessage>();
            var current = leaf;

            for (int i = 0; i < maxDepth; i++)
            {
                var refMsg = current.ReferencedMessage;
                if (refMsg == null && current.Reference?.Message.Id != null)
                    refMsg = await current.Channel.GetMessageAsync(current.Reference.Message.Id);
                if (refMsg == null) break;
                chain.Add(refMsg);
                current = refMsg;
            }

            return chain;
        }
    }
}
