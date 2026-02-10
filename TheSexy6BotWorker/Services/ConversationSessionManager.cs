using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

/// <summary>
/// Thread-safe manager for conversation sessions across channels
/// </summary>
public class ConversationSessionManager : IConversationSessionManager
{
    private readonly ConcurrentDictionary<ulong, ConversationSession> _sessions = new();
    private readonly ILogger<ConversationSessionManager> _logger;
    
    public ConversationSessionManager(ILogger<ConversationSessionManager> logger)
    {
        _logger = logger;
    }
    
    public ConversationSession? GetActiveSession(ulong channelId)
    {
        if (_sessions.TryGetValue(channelId, out var session))
        {
            if (session.IsActive)
            {
                return session;
            }
            
            // Session expired, remove it
            _logger.LogDebug(
                "Session expired for channel {ChannelId} after {Duration}",
                channelId, 
                session.Duration);
            
            _sessions.TryRemove(channelId, out _);
        }
        
        return null;
    }
    
    public ConversationSession StartSession(ulong channelId, IBotConfiguration bot)
    {
        var session = new ConversationSession(channelId, bot);
        
        // Replace any existing session
        _sessions.AddOrUpdate(
            channelId, 
            session, 
            (_, existing) =>
            {
                _logger.LogDebug(
                    "Replacing existing session for channel {ChannelId} (was {OldBot}, now {NewBot})",
                    channelId,
                    existing.Bot.Prefix,
                    bot.Prefix);
                return session;
            });
        
        _logger.LogInformation(
            "Started new {Bot} session in channel {ChannelId}",
            bot.Prefix,
            channelId);
        
        return session;
    }
    
    public ConversationSession GetOrCreateSession(ulong channelId, IBotConfiguration bot)
    {
        var existing = GetActiveSession(channelId);
        
        // If existing session is for a different bot, replace it
        if (existing != null && existing.Bot.ServiceId == bot.ServiceId)
        {
            existing.Touch();
            return existing;
        }
        
        return StartSession(channelId, bot);
    }
    
    public void AddMessage(ulong channelId, ChatMessageContent message)
    {
        if (_sessions.TryGetValue(channelId, out var session))
        {
            session.RecordMessage(message);
            
            _logger.LogDebug(
                "Added message to session in channel {ChannelId} (total: {Count}, tokens: ~{Tokens})",
                channelId,
                session.MessageCount,
                session.EstimatedTokens);
        }
    }
    
    public void EndSession(ulong channelId)
    {
        if (_sessions.TryRemove(channelId, out var session))
        {
            _logger.LogInformation(
                "Ended {Bot} session in channel {ChannelId} after {Duration} ({Messages} messages)",
                session.Bot.Prefix,
                channelId,
                session.Duration,
                session.MessageCount);
        }
    }
    
    public bool HasActiveSession(ulong channelId)
    {
        return GetActiveSession(channelId) != null;
    }
    
    public void CleanupExpiredSessions()
    {
        var expiredChannels = _sessions
            .Where(kvp => !kvp.Value.IsActive)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var channelId in expiredChannels)
        {
            if (_sessions.TryRemove(channelId, out var session))
            {
                _logger.LogDebug(
                    "Cleaned up expired session for channel {ChannelId}",
                    channelId);
            }
        }
        
        if (expiredChannels.Count > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} expired sessions",
                expiredChannels.Count);
        }
    }
}
