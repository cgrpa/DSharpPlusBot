using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Ardalis.GuardClauses;
using System.Text;
using System.Text.Json;
using System.Linq;
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
        private readonly ImageGenerationContextAccessor _imageGenerationContextAccessor;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private const int ManualContinuationMaxTurns = 3;
        private static readonly TimeSpan TypingKeepAliveInterval = TimeSpan.FromSeconds(8);
        private static readonly string AppVersion =
            $"{Environment.GetEnvironmentVariable("APP_VERSION") ?? "local"} — \"{Environment.GetEnvironmentVariable("APP_COMMIT_MSG") ?? "unknown"}\"";

        public MessageCreatedHandler(
            Kernel kernel,
            DynamicStatusService statusService,
            BotRegistry botRegistry,
            IConversationSessionManager sessionManager,
            ImageGenerationContextAccessor imageGenerationContextAccessor)
        {
            _kernel = Guard.Against.Null(kernel, nameof(kernel));
            _statusService = Guard.Against.Null(statusService, nameof(statusService));
            _botRegistry = Guard.Against.Null(botRegistry, nameof(botRegistry));
            _sessionManager = Guard.Against.Null(sessionManager, nameof(sessionManager));
            _imageGenerationContextAccessor = Guard.Against.Null(imageGenerationContextAccessor, nameof(imageGenerationContextAccessor));
        }

        public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
        {
            if (e.Author.IsBot) return;

            if (e.Message.Content.StartsWith("ping", StringComparison.OrdinalIgnoreCase))
            {
                await e.Message.RespondAsync("pong!");
                return;
            }

            if (MessageRoutingRules.IsLikelyCommandMessage(e.Message.Content))
            {
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
            using var imageGenerationScope = _imageGenerationContextAccessor.Push(
                new ImageGenerationExecutionContext(
                    e.Message.Id,
                    e.Channel.Id,
                    e.Author.Id,
                    IsAuto: true));

            await RunWithTypingIndicatorAsync(e.Channel, async () =>
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
                            "If you need to use a tool (search, weather, image generation, etc.) to inform your decision, do that now. " +
                            "Otherwise, just acknowledge with 'Ready to decide.'");

                        var toolResponseSettings = new OpenAIPromptExecutionSettings
                        {
                            MaxTokens = 256,
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        };
                        var toolResponse = await GetChatMessageWithManualFallbackAsync(chatService, chatHistory, toolResponseSettings);

                        if (!string.IsNullOrWhiteSpace(toolResponse.Content))
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

                    var decisionSettings = new OpenAIPromptExecutionSettings
                    {
                        MaxTokens = 4096,
                        ResponseFormat = typeof(EngagementDecision)
                    };
                    var response = await GetChatMessageWithManualFallbackAsync(chatService, chatHistory, decisionSettings);

                    var decision = JsonSerializer.Deserialize<EngagementDecision>(response.Content ?? "{}", JsonOptions);
                    var imageResult = _imageGenerationContextAccessor.ConsumeLastResult();

                    if (decision?.ShouldRespond == true)
                    {
                        var responseText = decision.Message ?? string.Empty;
                        DiscordEmbed? responseEmbed = null;

                        if (imageResult?.IsNewGeneration == true)
                        {
                            if (imageResult.Success)
                            {
                                responseText = ImageResponseFormatter.BuildContent(imageResult, responseText);
                                responseEmbed = ImageResponseFormatter.BuildEmbed(imageResult);
                            }
                            else if (string.IsNullOrWhiteSpace(responseText))
                            {
                                responseText = imageResult.ResponseText;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(responseText))
                        {
                            return;
                        }

                        session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, responseText));
                        if (imageResult?.Success == true && imageResult.IsNewGeneration && imageResult.HistoryMarker is not null)
                        {
                            session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, imageResult.HistoryMarker.ToJson()));
                        }

                        await DiscordMessageSender.SendChunkedAsync(e, responseText, responseEmbed);
                        await _statusService.RecordInteraction(e.Message.Content, responseText);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Engagement mode error: {ex.Message}");
                }
            });
        }

        private async Task ProcessBotMessageAsync(MessageCreatedEventArgs e, IBotConfiguration bot, string userMessage, ConversationSession? session)
        {
            using var imageGenerationScope = _imageGenerationContextAccessor.Push(
                new ImageGenerationExecutionContext(
                    e.Message.Id,
                    e.Channel.Id,
                    e.Author.Id,
                    IsAuto: bot.SupportsFunctionCalling));

            await RunWithTypingIndicatorAsync(e.Channel, async () =>
            {
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

                    var response = await GetChatMessageWithManualFallbackAsync(chatService, chatHistory, bot.Settings);
                    var responseContent = response.Content ?? string.Empty;
                    var imageResult = _imageGenerationContextAccessor.ConsumeLastResult();

                    DiscordEmbed? responseEmbed = null;
                    if (imageResult?.IsNewGeneration == true)
                    {
                        if (imageResult.Success)
                        {
                            responseContent = ImageResponseFormatter.BuildContent(imageResult, responseContent);
                            responseEmbed = ImageResponseFormatter.BuildEmbed(imageResult);
                        }
                        else if (string.IsNullOrWhiteSpace(responseContent))
                        {
                            responseContent = imageResult.ResponseText;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(responseContent))
                        throw new InvalidOperationException("The model did not return a final text response after tool execution.");

                    if (session != null)
                    {
                        session.RecordMessage(new ChatMessageContent(AuthorRole.User, currentMessage));
                        session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, responseContent));
                        if (imageResult?.Success == true && imageResult.IsNewGeneration && imageResult.HistoryMarker is not null)
                        {
                            session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, imageResult.HistoryMarker.ToJson()));
                        }
                    }

                    await DiscordMessageSender.SendChunkedAsync(e, responseContent, responseEmbed);

                    if (bot.SupportsFunctionCalling)
                        await _statusService.RecordInteraction(e.Message.Content, responseContent);
                }
                catch (Exception ex)
                {
                    await e.Message.RespondAsync($"❌ Error: {ex.Message}");
                }
            });
        }

        private async Task RunWithTypingIndicatorAsync(DiscordChannel channel, Func<Task> action)
        {
            using var keepAliveCts = new CancellationTokenSource();
            var typingTask = KeepTypingIndicatorAliveAsync(channel, keepAliveCts.Token);

            try
            {
                await action();
            }
            finally
            {
                keepAliveCts.Cancel();
                try
                {
                    await typingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when we cancel typing keepalive after finishing the response.
                }
            }
        }

        private async Task<ChatMessageContent> GetChatMessageWithManualFallbackAsync(
            IChatCompletionService chatService,
            ChatHistory chatHistory,
            PromptExecutionSettings executionSettings)
        {
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: _kernel);

            for (var turn = 0; turn < ManualContinuationMaxTurns; turn++)
            {
                var functionCalls = FunctionCallContent.GetFunctionCalls(response).ToList();
                if (functionCalls.Count == 0)
                {
                    return response;
                }

                chatHistory.Add(response);

                foreach (var functionCall in functionCalls)
                {
                    try
                    {
                        var functionResult = await functionCall.InvokeAsync(_kernel);
                        chatHistory.Add(new FunctionResultContent(functionCall, functionResult).ToChatMessage());
                    }
                    catch (Exception ex)
                    {
                        chatHistory.Add(new FunctionResultContent(functionCall, $"Tool execution failed: {ex.Message}").ToChatMessage());
                    }
                }

                response = await chatService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings: executionSettings,
                    kernel: _kernel);
            }

            return response;
        }

        private static async Task KeepTypingIndicatorAliveAsync(DiscordChannel channel, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await channel.TriggerTypingAsync();
                }
                catch
                {
                    // Typing indicator failures should not block response generation.
                }

                await Task.Delay(TypingKeepAliveInterval, cancellationToken);
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

            systemMessage.AppendLine();
            systemMessage.AppendLine();
            systemMessage.Append($"Deployed version: {AppVersion}");

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
