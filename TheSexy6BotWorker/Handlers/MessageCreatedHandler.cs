using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Ardalis.GuardClauses;
using Microsoft.SemanticKernel.ChatCompletion;
using DSharpPlus.Entities;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
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
        private const int MaxMessageLength = 1980;

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
            // Ignore bot messages
            if (e.Author.IsBot) return;
            
            // Handle simple ping command
            if (e.Message.Content.StartsWith("ping", StringComparison.OrdinalIgnoreCase))
            {
                await e.Message.RespondAsync("pong!");
                return;
            }

            // Check if message matches any registered bot prefix
            if (_botRegistry.TryGetBot(e.Message.Content, out var bot, out var strippedMessage))
            {
                // Direct invocation - must respond
                await ProcessDirectInvocationAsync(e, bot!, strippedMessage);
                return;
            }

            // Check for active engagement session in this channel
            var session = _sessionManager.GetActiveSession(e.Channel.Id);
            if (session != null && session.Bot.SupportsEngagementMode)
            {
                await ProcessEngagementMessageAsync(e, session);
            }
        }

        /// <summary>
        /// Handles direct bot invocation (message starts with prefix) - bot MUST respond
        /// </summary>
        private async Task ProcessDirectInvocationAsync(
            MessageCreatedEventArgs e, 
            IBotConfiguration bot, 
            string strippedMessage)
        {
            // Start or continue engagement session if bot supports it
            if (bot.SupportsEngagementMode)
            {
                var session = _sessionManager.GetOrCreateSession(e.Channel.Id, bot);
                await ProcessBotMessageAsync(e, bot, strippedMessage, session);
            }
            else
            {
                // No engagement mode - just process the message
                await ProcessBotMessageAsync(e, bot, strippedMessage, session: null);
            }
        }

        /// <summary>
        /// Handles messages in an active engagement session - bot decides whether to respond via tool call
        /// </summary>
        private async Task ProcessEngagementMessageAsync(
            MessageCreatedEventArgs e,
            Models.ConversationSession session)
        {
            // Record the message in session context
            var userMessage = new ChatMessageContent(AuthorRole.User, FormatMessageWithUsername(e.Message));
            session.RecordMessage(userMessage);
            
            // Apply rate limiting delay if high activity
            var delay = session.GetReplyDelay();
            if (delay.HasValue)
            {
                await Task.Delay(delay.Value);
            }
            
            await ProcessEngagementBotMessageAsync(e, session.Bot, e.Message.Content, session);
        }

        /// <summary>
        /// Process message in engagement mode using a two-phase approach:
        /// 1. Allow the bot to use tools (search, weather) with auto function calling
        /// 2. Get a structured decision on whether to respond
        /// </summary>
        private async Task ProcessEngagementBotMessageAsync(
            MessageCreatedEventArgs e,
            IBotConfiguration bot,
            string userMessage,
            ConversationSession session)
        {
            try
            {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>(serviceKey: bot.ServiceId);
                var chatHistory = BuildChatHistory(bot, session);
                
                var currentMessage = FormatMessageWithAttachments(e.Message);

                if (bot.SupportsImages)
                {
                    await AddImageMessagesAsync(chatHistory, e.Message);
                }

                // Phase 1: Let the bot use tools if needed (search, weather, etc.)
                if (bot.SupportsFunctionCalling)
                {
                    chatHistory.AddUserMessage(
                        $"[NEW MESSAGE IN CHANNEL]\n{currentMessage}\n\n" +
                        "[INSTRUCTION] Do NOT respond to this message yet. " +
                        "If you need to look something up (search, weather, etc.) to inform your decision, do that now. " +
                        "Otherwise, just acknowledge with 'Ready to decide.'");
                    
                    var toolSettings = new OpenAIPromptExecutionSettings
                    {
                        MaxTokens = 256,
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    };
                    
                    var toolResponse = await chatCompletionService.GetChatMessageContentAsync(
                        chatHistory,
                        kernel: _kernel,
                        executionSettings: toolSettings);

                    if (!string.IsNullOrEmpty(toolResponse.Content))
                    {
                        chatHistory.AddAssistantMessage(toolResponse.Content);
                    }
                }
                else
                {
                    chatHistory.AddUserMessage($"[NEW MESSAGE IN CHANNEL]\n{currentMessage}");
                }

                // Phase 2: Get structured decision on whether to respond
                chatHistory.AddUserMessage(
                    "Now decide: Do you want to respond to this message? " +
                    "Consider if you have something valuable, funny, or interesting to add. " +
                    "Return JSON with 'shouldRespond' (boolean) and 'message' (your response text if shouldRespond is true).");

                var decisionSettings = new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 4096,
                    ResponseFormat = typeof(EngagementDecision)
                };
                
                var response = await chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    kernel: _kernel,
                    executionSettings: decisionSettings);

                var decision = JsonSerializer.Deserialize<EngagementDecision>(
                    response.Content ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (decision?.ShouldRespond == true && !string.IsNullOrWhiteSpace(decision.Message))
                {
                    await e.Channel.TriggerTypingAsync();
                    
                    session.RecordMessage(new ChatMessageContent(AuthorRole.User, currentMessage));
                    session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, decision.Message));

                    await SendChunkedMessageAsync(e, decision.Message);
                    await _statusService.RecordInteraction(e.Message.Content, decision.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Engagement mode error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process message for direct invocation - bot MUST respond.
        /// Used when user explicitly calls the bot with a prefix.
        /// </summary>
        private async Task ProcessBotMessageAsync(
            MessageCreatedEventArgs e, 
            IBotConfiguration bot, 
            string userMessage,
            Models.ConversationSession? session)
        {
            // Direct invocation - always show typing
            await e.Channel.TriggerTypingAsync();

            try
            {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>(serviceKey: bot.ServiceId);
                var chatHistory = BuildChatHistory(bot, session);
                
                // Build conversation history for bots that support reply chains (when not in session)
                if (session == null && bot.SupportsReplyChains && e.Message.ReferencedMessage != null)
                {
                    await AddReplyChainHistoryAsync(chatHistory, e.Message, bot);
                }

                // Add current message
                var currentMessage = FormatMessageWithAttachments(e.Message);
                chatHistory.AddUserMessage(currentMessage);

                // Add images from current message if bot supports them
                if (bot.SupportsImages)
                {
                    await AddImageMessagesAsync(chatHistory, e.Message);
                }

                // Get AI response with bot-specific settings
                var response = await chatCompletionService.GetChatMessageContentAsync(
                    chatHistory, 
                    kernel: _kernel, 
                    executionSettings: bot.Settings);

                var responseContent = response.Content ?? string.Empty;
                
                // Record bot response in session if active
                if (session != null)
                {
                    session.RecordMessage(new ChatMessageContent(AuthorRole.User, currentMessage));
                    session.RecordMessage(new ChatMessageContent(AuthorRole.Assistant, responseContent));
                }

                // Send response
                await SendChunkedMessageAsync(e, responseContent);

                // Record interaction for dynamic status (if bot supports function calling)
                if (bot.SupportsFunctionCalling)
                {
                    await _statusService.RecordInteraction(e.Message.Content, responseContent);
                }
            }
            catch (Exception ex)
            {
                await e.Message.RespondAsync($"❌ Error: {ex.Message}");
            }
        }

        private static ChatHistory BuildChatHistory(IBotConfiguration bot, Models.ConversationSession? session)
        {
            var chatHistory = new ChatHistory();
            
            // Build system message
            var systemMessage = new StringBuilder(bot.SystemMessage);
            
            // Add configuration description
            systemMessage.AppendLine();
            systemMessage.AppendLine();
            systemMessage.Append(bot.GetConfigurationDescription());
            
            // Add engagement mode instructions if in session
            if (session != null && !string.IsNullOrEmpty(bot.EngagementModeInstructions))
            {
                systemMessage.AppendLine();
                systemMessage.AppendLine();
                systemMessage.Append(bot.EngagementModeInstructions);
                systemMessage.AppendLine();
                systemMessage.AppendLine($"Session context: {session.MessageCount} messages over {FormatDuration(session.Duration)}.");
            }
            
            chatHistory.AddSystemMessage(systemMessage.ToString());
            
            // Add session history if available
            if (session != null)
            {
                foreach (var message in session.History)
                {
                    chatHistory.Add(message);
                }
            }
            
            return chatHistory;
        }

        private async Task AddReplyChainHistoryAsync(ChatHistory chatHistory, DiscordMessage message, IBotConfiguration bot)
        {
            var replyChain = await GetReplyChainAsync(message);
            replyChain.Reverse();

            foreach (var msg in replyChain)
            {
                if (msg.Author.IsBot)
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
                else
                {
                    var formattedMessage = FormatMessageWithAttachments(msg);
                    chatHistory.AddUserMessage(formattedMessage);

                    if (bot.SupportsImages)
                    {
                        await AddImageMessagesAsync(chatHistory, msg);
                    }
                }
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
                return $"{(int)duration.TotalSeconds} seconds";
            if (duration.TotalHours < 1)
                return $"{(int)duration.TotalMinutes} minutes";
            return $"{duration.TotalHours:F1} hours";
        }

        private static string FormatMessageWithUsername(DiscordMessage message)
        {
            return $"{message.Author.Username}: {FormatMessageWithAttachments(message)}";
        }
        
        private static async Task SendChunkedMessageAsync(MessageCreatedEventArgs e, string content)
        {

            if (content.Length <= MaxMessageLength)
            {
                await e.Message.RespondAsync(content);
                return;
            }


            int totalChunks = (int)Math.Ceiling((double)content.Length / MaxMessageLength);

            for (int i = 0; i < totalChunks; i++)
            {
                int startIndex = i * MaxMessageLength;
                int length = Math.Min(MaxMessageLength, content.Length - startIndex);
                string chunk = content.Substring(startIndex, length);


                string prefix = i > 0 ? "⤴️ " : "";
                string suffix = i < totalChunks - 1 ? " ⤵️" : "";

                await e.Message.RespondAsync($"{prefix}{chunk}{suffix}");
            }
        }
        
        async Task<List<DiscordMessage>> GetReplyChainAsync(DiscordMessage leaf, int maxDepth = 10)
        {
            var chain = new List<DiscordMessage>();
            var current = leaf;

            for (int i = 0; i < maxDepth; i++)
            {
                var refMsg = current.ReferencedMessage;

                if (refMsg == null && current.Reference?.Message.Id != null)
                {
                    refMsg = await current.Channel.GetMessageAsync(current.Reference.Message.Id);
                }

                if (refMsg == null)
                    break;

                chain.Add(refMsg);
                current = refMsg;
            }

            return chain;
        }

        private static string FormatMessageWithAttachments(DiscordMessage message, string prefix = "")
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append($"{prefix}{message.Content}");

            // Add attachment URLs
            if (message.Attachments.Any())
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("[Attachments:]");
                foreach (var attachment in message.Attachments)
                {
                    messageBuilder.AppendLine($"- {attachment.FileName}: {attachment.Url}");
                }
            }

            // Add embed information
            if (message.Embeds.Any())
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("[Embeds:]");
                foreach (var embed in message.Embeds)
                {
                    if (!string.IsNullOrEmpty(embed.Title))
                        messageBuilder.AppendLine($"- Title: {embed.Title}");
                    if (!string.IsNullOrEmpty(embed.Description))
                        messageBuilder.AppendLine($"- Description: {embed.Description}");
                    if (embed.Url != null)
                        messageBuilder.AppendLine($"- URL: {embed.Url}");
                    if (embed.Image?.Url != null)
                        messageBuilder.AppendLine($"- Image: {embed.Image.Url}");
                    if (embed.Thumbnail?.Url != null)
                        messageBuilder.AppendLine($"- Thumbnail: {embed.Thumbnail.Url}");
                }
            }

            return messageBuilder.ToString();
        }

        private static async Task AddImageMessagesAsync(ChatHistory chatHistory, DiscordMessage message)
        {
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

            foreach (var attachment in message.Attachments)
            {
                // Check if it's an image
                var extension = Path.GetExtension(attachment.FileName).ToLower();
                if (!imageExtensions.Contains(extension))
                    continue;

                try
                {
                    // Try using the URL directly first (some APIs prefer this)
                    var imageContent = new ChatMessageContentItemCollection
                    {
                        new Microsoft.SemanticKernel.ImageContent(new Uri(attachment.Url))
                    };

                    chatHistory.AddUserMessage(imageContent);
                }
                catch (Exception)
                {
                    // If direct URL doesn't work, try base64
                    try
                    {
                        using var httpClient = new HttpClient();
                        var imageBytes = await httpClient.GetByteArrayAsync(attachment.Url);
                        var base64Image = Convert.ToBase64String(imageBytes);

                        // Determine MIME type
                        var mimeType = extension switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            ".bmp" => "image/bmp",
                            _ => "image/jpeg"
                        };

                        var imageContentBase64 = new ChatMessageContentItemCollection
                        {
                            new Microsoft.SemanticKernel.ImageContent(new Uri($"data:{mimeType};base64,{base64Image}"))
                        };

                        chatHistory.AddUserMessage(imageContentBase64);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            // Also check embeds for images
            foreach (var embed in message.Embeds)
            {
                var imageUrl = embed.Image?.Url?.ToString() ?? embed.Thumbnail?.Url?.ToString();
                if (string.IsNullOrEmpty(imageUrl))
                    continue;

                try
                {
                    // Try direct URL first
                    var imageContent = new ChatMessageContentItemCollection
                    {
                        new Microsoft.SemanticKernel.ImageContent(new Uri(imageUrl))
                    };

                    chatHistory.AddUserMessage(imageContent);
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

    }
}
