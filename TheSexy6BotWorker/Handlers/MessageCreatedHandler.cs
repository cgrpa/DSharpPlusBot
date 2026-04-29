using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Ardalis.GuardClauses;
using System.Text;
using System.Text.Json;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Helpers;
using TheSexy6BotWorker.Models;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Handlers
{
    public class MessageCreatedHandler : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly Kernel _kernel;
        private readonly DynamicStatusService _statusService;
        private readonly BotRegistry _botRegistry;
        private readonly IConversationSessionManager _sessionManager;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public MessageCreatedHandler(
            Kernel kernel,
            DynamicStatusService statusService,
            BotRegistry botRegistry,
            IConversationSessionManager sessionManager)
        {
            _kernel = Guard.Against.Null(kernel, nameof(kernel));
            _statusService = Guard.Against.Null(statusService, nameof(statusService));
            _botRegistry = Guard.Against.Null(botRegistry, nameof(botRegistry));
            _sessionManager = Guard.Against.Null(sessionManager, nameof(sessionManager));
        }

        public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
        {
            if (e.Author.IsBot) return;

            if (e.Message.Content.StartsWith("ping", StringComparison.OrdinalIgnoreCase))
            {
                await e.Message.RespondAsync("pong!");
                return;
            }

            if (_botRegistry.TryGetBot(e.Message.Content, out var bot, out var strippedMessage))
            {
                var session = bot!.SupportsEngagementMode
                    ? _sessionManager.GetOrCreateSession(e.Channel.Id, bot)
                    : null;
                await ProcessBotMessageAsync(e, bot!, strippedMessage, session);
                return;
            }

            var activeSession = _sessionManager.GetActiveSession(e.Channel.Id);
            if (activeSession?.Bot.SupportsEngagementMode == true)
                await ProcessEngagementMessageAsync(e, activeSession);
        }

        private async Task ProcessEngagementMessageAsync(MessageCreatedEventArgs e, ConversationSession session)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.User, DiscordMessageFormatter.FormatWithUsername(e.Message)));

            var delay = session.GetReplyDelay();
            if (delay.HasValue) await Task.Delay(delay.Value);

            await ProcessEngagementBotMessageAsync(e, session.Bot, session);
        }

        private async Task ProcessEngagementBotMessageAsync(MessageCreatedEventArgs e, IBotConfiguration bot, ConversationSession session)
        {
            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>(serviceKey: bot.ServiceId);
                var chatHistory = BuildChatHistory(bot, session);
                var currentMessage = DiscordMessageFormatter.FormatWithUsername(e.Message);

                if (bot.SupportsImages)
                    await DiscordMessageFormatter.AddImagesToHistoryAsync(chatHistory, e.Message);

                if (bot.SupportsFunctionCalling)
                {
                    chatHistory.AddUserMessage(
                        $"[NEW MESSAGE IN CHANNEL]\n{currentMessage}\n\n" +
                        "[INSTRUCTION] Do NOT respond to this message yet. " +
                        "If you need to look something up (search, weather, etc.) to inform your decision, do that now. " +
                        "Otherwise, just acknowledge with 'Ready to decide.'");

                    var toolResponse = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel,
                        executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 256, FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() });

                    if (!string.IsNullOrEmpty(toolResponse.Content))
                        chatHistory.AddAssistantMessage(toolResponse.Content);
                }
                else
                {
                    chatHistory.AddUserMessage($"[NEW MESSAGE IN CHANNEL]\n{currentMessage}");
                }

                chatHistory.AddUserMessage(
                    "Now decide: Do you want to respond to this message? " +
                    "Consider if you have something valuable, funny, or interesting to add. " +
                    "Return JSON with 'shouldRespond' (boolean) and 'message' (your response text if shouldRespond is true).");

                var response = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel,
                    executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 4096, ResponseFormat = typeof(EngagementDecision) });

                var decision = JsonSerializer.Deserialize<EngagementDecision>(response.Content ?? "{}", JsonOptions);

                if (decision?.ShouldRespond == true && !string.IsNullOrWhiteSpace(decision.Message))
                {
                    await e.Channel.TriggerTypingAsync();
                    session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, decision.Message));
                    await DiscordMessageSender.SendChunkedAsync(e, decision.Message);
                    await _statusService.RecordInteraction(e.Message.Content, decision.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Engagement mode error: {ex.Message}");
            }
        }

        private async Task ProcessBotMessageAsync(MessageCreatedEventArgs e, IBotConfiguration bot, string userMessage, ConversationSession? session)
        {
            await e.Channel.TriggerTypingAsync();
            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>(serviceKey: bot.ServiceId);
                var chatHistory = BuildChatHistory(bot, session);

                if (session == null && bot.SupportsReplyChains && e.Message.ReferencedMessage != null)
                    await DiscordReplyChainHelper.AddToHistoryAsync(chatHistory, e.Message, bot);

                var currentMessage = DiscordMessageFormatter.FormatWithUsername(e.Message);
                chatHistory.AddUserMessage(currentMessage);

                if (bot.SupportsImages)
                    await DiscordMessageFormatter.AddImagesToHistoryAsync(chatHistory, e.Message);

                var response = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel, executionSettings: bot.Settings);
                var responseContent = response.Content ?? string.Empty;

                if (session != null)
                {
                    session.RecordMessage(new ChatMessageContent(AuthorRole.User, currentMessage));
                    session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, responseContent));
                }

                await DiscordMessageSender.SendChunkedAsync(e, responseContent);

                if (bot.SupportsFunctionCalling)
                    await _statusService.RecordInteraction(e.Message.Content, responseContent);
            }
            catch (Exception ex)
            {
                await e.Message.RespondAsync($"❌ Error: {ex.Message}");
            }
        }

        private static ChatHistory BuildChatHistory(IBotConfiguration bot, ConversationSession? session)
        {
            var chatHistory = new ChatHistory();
            var systemMessage = new StringBuilder(bot.SystemMessage);

            systemMessage.AppendLine();
            systemMessage.AppendLine();
            systemMessage.Append(bot.GetConfigurationDescription());

            if (session != null && !string.IsNullOrEmpty(bot.EngagementModeInstructions))
            {
                systemMessage.AppendLine();
                systemMessage.AppendLine();
                systemMessage.Append(bot.EngagementModeInstructions);
                systemMessage.AppendLine();
                systemMessage.AppendLine($"Session context: {session.MessageCount} messages over {DiscordMessageFormatter.FormatDuration(session.Duration)}.");
            }

            chatHistory.AddSystemMessage(systemMessage.ToString());

            if (session != null)
            {
                foreach (var message in session.History)
                    chatHistory.Add(message);
            }

            return chatHistory;
        }
    }
}
