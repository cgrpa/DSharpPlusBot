using System.Text;
using DSharpPlus.Commands;
using Microsoft.SemanticKernel;

namespace TheSexy6BotWorker.Commands
{
    public static class ToolsCommand
    {
        [Command("tools")]
        public static ValueTask ExecuteToolsAsync(CommandContext context) =>
            ExecuteAsync(context);

        [Command("plugins")]
        public static ValueTask ExecutePluginsAsync(CommandContext context) =>
            ExecuteAsync(context);

        private static async ValueTask ExecuteAsync(CommandContext context)
        {
            var kernel = context.ServiceProvider.GetService(typeof(Kernel)) as Kernel;
            if (kernel == null)
            {
                await context.RespondAsync("❌ Kernel is unavailable, so callable tools cannot be listed right now.");
                return;
            }

            var toolMetadata = KernelPluginExtensions
                .GetFunctionsMetadata(kernel.Plugins)
                .OrderBy(static metadata => metadata.PluginName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static metadata => metadata.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (toolMetadata.Length == 0)
            {
                await context.RespondAsync("No callable LLM tools are currently registered.");
                return;
            }

            var groupedByPlugin = toolMetadata
                .GroupBy(
                    static metadata => string.IsNullOrWhiteSpace(metadata.PluginName) ? "(unnamed plugin)" : metadata.PluginName,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var message = new StringBuilder();
            message.AppendLine($"Callable LLM tools: {toolMetadata.Length} across {groupedByPlugin.Length} plugin(s).");
            message.AppendLine("Scope: Kernel callable tools only (not bot text commands).");

            foreach (var pluginGroup in groupedByPlugin)
            {
                message.AppendLine();
                message.AppendLine($"{pluginGroup.Key} ({pluginGroup.Count()}):");

                foreach (var tool in pluginGroup)
                {
                    message.AppendLine($"- {tool.Name}");
                }
            }

            await SendChunkedAsync(context, message.ToString());
        }

        private static async ValueTask SendChunkedAsync(
            CommandContext context,
            string content,
            int maxChunkLength = 1900)
        {
            if (content.Length <= maxChunkLength)
            {
                await context.RespondAsync(content);
                return;
            }

            var chunks = SplitIntoChunks(content, maxChunkLength);
            await context.RespondAsync(chunks[0]);

            for (var i = 1; i < chunks.Count; i++)
            {
                await context.FollowupAsync(chunks[i]);
            }
        }

        private static List<string> SplitIntoChunks(string content, int maxChunkLength)
        {
            var chunks = new List<string>();
            var currentChunk = new StringBuilder();

            foreach (var line in content.Split('\n'))
            {
                var normalizedLine = line.TrimEnd('\r');
                var lineWithNewLine = normalizedLine + Environment.NewLine;

                if (lineWithNewLine.Length > maxChunkLength)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().TrimEnd());
                        currentChunk.Clear();
                    }

                    var remaining = normalizedLine;
                    while (remaining.Length > 0)
                    {
                        var take = Math.Min(maxChunkLength, remaining.Length);
                        chunks.Add(remaining[..take]);
                        remaining = remaining[take..];
                    }

                    continue;
                }

                if (currentChunk.Length + lineWithNewLine.Length > maxChunkLength)
                {
                    chunks.Add(currentChunk.ToString().TrimEnd());
                    currentChunk.Clear();
                }

                currentChunk.Append(lineWithNewLine);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().TrimEnd());
            }

            return chunks;
        }
    }
}
