using Ardalis.GuardClauses;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Services;

public class BotRegistry
{
    private readonly Dictionary<string, IBotConfiguration> _bots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a bot configuration with the registry
    /// </summary>
    public void Register(IBotConfiguration bot)
    {
        Guard.Against.Null(bot, nameof(bot));
        Guard.Against.NullOrWhiteSpace(bot.Prefix, nameof(bot.Prefix));

        if (_bots.ContainsKey(bot.Prefix))
        {
            throw new InvalidOperationException($"A bot with prefix '{bot.Prefix}' is already registered.");
        }

        _bots[bot.Prefix] = bot;
    }

    /// <summary>
    /// Attempts to find a bot configuration matching the message prefix
    /// </summary>
    /// <param name="messageContent">The full message content</param>
    /// <param name="bot">The matching bot configuration, if found</param>
    /// <param name="strippedMessage">The message with the prefix removed</param>
    /// <returns>True if a matching bot was found</returns>
    public bool TryGetBot(string messageContent, out IBotConfiguration? bot, out string strippedMessage)
    {
        Guard.Against.NullOrWhiteSpace(messageContent, nameof(messageContent));

        bot = null;
        strippedMessage = messageContent;

        foreach (var kvp in _bots)
        {
            var prefix = kvp.Key;
            
            // Check if message starts with this bot's prefix (case-insensitive)
            if (messageContent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                bot = kvp.Value;
                
                // Strip the prefix and any leading whitespace
                strippedMessage = messageContent.Substring(prefix.Length).TrimStart();
                
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all registered bot configurations
    /// </summary>
    public IReadOnlyCollection<IBotConfiguration> GetAllBots() => _bots.Values.ToList().AsReadOnly();

    /// <summary>
    /// Gets a bot configuration by prefix
    /// </summary>
    public IBotConfiguration? GetBot(string prefix) => _bots.GetValueOrDefault(prefix);
}
