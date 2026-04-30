using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Models;

/// <summary>
/// Represents an active conversation session in a channel
/// </summary>
public class ConversationSession
{
    private readonly Queue<DateTimeOffset> _recentMessageTimestamps = new();
    private readonly object _lock = new();
    
    public ConversationSession(ulong channelId, IBotConfiguration bot)
    {
        ChannelId = channelId;
        Bot = bot;
        CreatedAt = DateTimeOffset.UtcNow;
        LastActivity = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// The Discord channel ID this session is for
    /// </summary>
    public ulong ChannelId { get; }
    
    /// <summary>
    /// The bot configuration for this session
    /// </summary>
    public IBotConfiguration Bot { get; }
    
    /// <summary>
    /// The chat history for this session
    /// </summary>
    public ChatHistory History { get; } = new();
    
    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; }
    
    /// <summary>
    /// Last activity timestamp (updated on each message)
    /// </summary>
    public DateTimeOffset LastActivity { get; private set; }
    
    /// <summary>
    /// Number of messages in this session
    /// </summary>
    public int MessageCount { get; private set; }
    
    /// <summary>
    /// Estimated token count for context management
    /// </summary>
    public int EstimatedTokens { get; private set; }
    
    /// <summary>
    /// Whether this session is still active (not expired)
    /// </summary>
    public bool IsActive => DateTimeOffset.UtcNow - LastActivity < Bot.SessionTimeout;
    
    /// <summary>
    /// Whether the channel is experiencing high message activity
    /// </summary>
    public bool IsHighActivity
    {
        get
        {
            lock (_lock)
            {
                PruneOldTimestamps();
                return _recentMessageTimestamps.Count >= Bot.HighActivityThreshold;
            }
        }
    }
    
    /// <summary>
    /// Duration since session started
    /// </summary>
    public TimeSpan Duration => DateTimeOffset.UtcNow - CreatedAt;
    
    /// <summary>
    /// Records a new message in the session
    /// </summary>
    public void RecordMessage(ChatMessageContent message, int estimatedTokens = 0)
    {
        lock (_lock)
        {
            History.Add(message);
            MessageCount++;
            EstimatedTokens += estimatedTokens > 0 ? estimatedTokens : EstimateTokens(message.Content ?? "");
            LastActivity = DateTimeOffset.UtcNow;
            _recentMessageTimestamps.Enqueue(DateTimeOffset.UtcNow);
            PruneOldTimestamps();
        }
    }
    
    /// <summary>
    /// Gets the delay that should be applied before responding (if high activity)
    /// </summary>
    public TimeSpan? GetReplyDelay()
    {
        if (!IsHighActivity) return null;
        
        var minMs = (int)Bot.HighActivityDelayMin.TotalMilliseconds;
        var maxMs = (int)Bot.HighActivityDelayMax.TotalMilliseconds;
        
        return TimeSpan.FromMilliseconds(Random.Shared.Next(minMs, maxMs + 1));
    }
    
    /// <summary>
    /// Touches the session to keep it alive
    /// </summary>
    public void Touch()
    {
        LastActivity = DateTimeOffset.UtcNow;
    }
    
    private void PruneOldTimestamps()
    {
        var cutoff = DateTimeOffset.UtcNow - Bot.HighActivityWindow;
        while (_recentMessageTimestamps.Count > 0 && _recentMessageTimestamps.Peek() < cutoff)
        {
            _recentMessageTimestamps.Dequeue();
        }
    }
    
    /// <summary>
    /// Simple token estimation (roughly 1.3 tokens per word)
    /// </summary>
    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(wordCount * 1.3);
    }
}
