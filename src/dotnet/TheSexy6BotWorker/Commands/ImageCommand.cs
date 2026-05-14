using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Helpers;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Commands;

public static class ImageCommand
{
    [Command("image")]
    public static async ValueTask ExecuteAsync(
        TextCommandContext context,
        string? firstToken = null,
        [RemainingText] string? remainingText = null)
    {
        var parsed = ImageCommandParser.Parse(firstToken, remainingText);
        if (!parsed.Success || parsed.Request is null)
        {
            await context.RespondAsync($"❌ {parsed.ErrorMessage}\nUsage: {parsed.Usage}");
            return;
        }

        var imageService = context.ServiceProvider.GetRequiredService<ImageGenerationService>();
        var contextAccessor = context.ServiceProvider.GetRequiredService<ImageGenerationContextAccessor>();
        var sessionManager = context.ServiceProvider.GetRequiredService<IConversationSessionManager>();
        var session = sessionManager.GetActiveSession(context.Channel.Id);

        using var scope = contextAccessor.Push(
            new ImageGenerationExecutionContext(
                context.Message.Id,
                context.Channel.Id,
                context.User.Id,
                IsAuto: false));

        if (session is not null)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.User, context.Message.Content));
        }

        await context.DeferResponseAsync();

        var result = await imageService.GenerateAsync(parsed.Request).ConfigureAwait(false);

        if (!result.IsNewGeneration)
        {
            await context.DeleteResponseAsync().ConfigureAwait(false);
            return;
        }

        if (!result.Success)
        {
            await context.EditResponseAsync($"❌ {result.ResponseText}").ConfigureAwait(false);
            return;
        }

        var content = ImageResponseFormatter.BuildContent(result);
        var embed = ImageResponseFormatter.BuildEmbed(result);
        if (result.ImageBytes is not null && result.ImageBytes.Length > 0)
        {
            var fileName = string.IsNullOrWhiteSpace(result.BlobName) ? "generated-image.png" : result.BlobName;
            await using var stream = new MemoryStream(result.ImageBytes, writable: false);
            var builder = new DiscordMessageBuilder()
                .WithContent(content)
                .AddEmbed(embed)
                .AddFile(fileName, stream);

            await context.EditResponseAsync(builder).ConfigureAwait(false);
        }
        else
        {
            await context.EditResponseAsync(content, embed).ConfigureAwait(false);
        }

        if (session is not null && result.HistoryMarker is not null)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, content));
            session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, result.HistoryMarker.ToJson()));
        }
    }
}
