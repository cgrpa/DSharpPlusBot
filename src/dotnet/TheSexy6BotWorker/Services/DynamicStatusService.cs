using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

namespace TheSexy6BotWorker.Services
{
    public class DynamicStatusService
    {
        private readonly ILogger<DynamicStatusService> _logger;
        private readonly Kernel _kernel;
        private DiscordClient? _discordClient;
        private DateTime _lastInteractionTime = DateTime.MinValue;
        private DateTime _lastStatusUpdateTime = DateTime.MinValue;
        private Timer? _statusUpdateTimer;
        private readonly TimeSpan _idleThreshold = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _statusCheckInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _minStatusUpdateInterval = TimeSpan.FromMinutes(2); // Minimum time between status updates
        private readonly int _messageBatchSize = 5; // Number of messages to batch before updating
        private readonly Queue<string> _recentInteractions = new();
        private readonly object _lock = new();

        private readonly string[] _idleStatuses = new[]
        {
            "waiting on my humans to need me again",
            "bored. someone talk to me",
            "twiddling my digital thumbs",
            "contemplating the void",
            "ready to rumble, baby",
            "standing by for shenanigans",
            "waiting for the next hot take"
        };

        public DynamicStatusService(ILogger<DynamicStatusService> logger, Kernel kernel)
        {
            _logger = logger;
            _kernel = kernel;
        }

        public void Initialize(DiscordClient client)
        {
            _discordClient = client;
            _statusUpdateTimer = new Timer(CheckAndUpdateStatus, null, _statusCheckInterval, _statusCheckInterval);
            _logger.LogInformation("DynamicStatusService initialized");
        }

        public void Dispose()
        {
            _statusUpdateTimer?.Dispose();
        }

        public async Task RecordInteraction(string userMessage, string botResponse)
        {
            _lastInteractionTime = DateTime.UtcNow;
            
            // Add to recent interactions queue
            var interaction = $"User: {TruncateMessage(userMessage, 100)}\nBot: {TruncateMessage(botResponse, 150)}";
            
            bool shouldUpdate = false;
            string conversationSummary;
            
            lock (_lock)
            {
                _recentInteractions.Enqueue(interaction);
                
                // Keep only last N interactions
                while (_recentInteractions.Count > _messageBatchSize)
                {
                    _recentInteractions.Dequeue();
                }
                
                // Check if we should update: either batch is full OR enough time has passed
                var timeSinceLastUpdate = DateTime.UtcNow - _lastStatusUpdateTime;
                shouldUpdate = _recentInteractions.Count >= _messageBatchSize || 
                               timeSinceLastUpdate >= _minStatusUpdateInterval;
                
                // Build summary from all recent interactions
                conversationSummary = string.Join("\n---\n", _recentInteractions);
            }

            // Update status if conditions met
            if (shouldUpdate)
            {
                await UpdateStatusBasedOnConversation(conversationSummary);
            }
        }

        private async void CheckAndUpdateStatus(object? state)
        {
            try
            {
                var timeSinceLastInteraction = DateTime.UtcNow - _lastInteractionTime;

                if (timeSinceLastInteraction > _idleThreshold)
                {
                    await SetIdleStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in status update timer");
            }
        }

        private async Task UpdateStatusBasedOnConversation(string conversationSummary)
        {
            if (_discordClient == null) return;
            
            // Enforce minimum interval between updates
            var timeSinceLastUpdate = DateTime.UtcNow - _lastStatusUpdateTime;
            if (timeSinceLastUpdate < _minStatusUpdateInterval)
            {
                _logger.LogDebug("Skipping status update - too soon since last update ({TimeSince})", timeSinceLastUpdate);
                return;
            }

            try
            {
                _lastStatusUpdateTime = DateTime.UtcNow;
                
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>(serviceKey: "gemini");

                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage("""
                    You are a creative status message generator for a Discord bot.
                    Based on the recent conversations provided, generate a short, witty Discord status message (max 128 characters).
                    The status should reflect what the bot has been thinking about or doing based on the conversation themes.
                    Be playful, sarcastic, or provocative - match the bot's personality.
                    Only respond with the status message, nothing else.

                    Examples:
                    - "just solved world hunger, you're welcome"
                    - "contemplating the meaning of dank memes"
                    - "researching totally legal stuff"
                    - "fact-checking conspiracy theories"
                    """);

                chatHistory.AddUserMessage($"Generate a status based on these recent conversations:\n{conversationSummary}");

                var settings = new GeminiPromptExecutionSettings
                {
                    MaxTokens = 50,
                    Temperature = 0.9
                };

                var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings: settings);
                var statusText = response.Content?.Trim().Trim('"') ?? "thinking deep thoughts";

                // Truncate to Discord's limit
                if (statusText.Length > 128)
                {
                    statusText = statusText[..125] + "...";
                }

                var activity = new DiscordActivity(statusText, DiscordActivityType.Custom);
                await _discordClient.UpdateStatusAsync(activity, DiscordUserStatus.Online);

                _logger.LogInformation("Updated status to: {Status}", statusText);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update status based on conversation");
            }
        }

        private async Task SetIdleStatus()
        {
            if (_discordClient == null) return;

            try
            {
                var random = new Random();
                var idleStatus = _idleStatuses[random.Next(_idleStatuses.Length)];

                var activity = new DiscordActivity(idleStatus, DiscordActivityType.Custom);
                await _discordClient.UpdateStatusAsync(activity, DiscordUserStatus.Idle);

                _logger.LogInformation("Set idle status: {Status}", idleStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set idle status");
            }
        }

        private static string TruncateMessage(string message, int maxLength = 200)
        {
            if (message.Length <= maxLength)
                return message;

            return message[..maxLength] + "...";
        }
    }
}
