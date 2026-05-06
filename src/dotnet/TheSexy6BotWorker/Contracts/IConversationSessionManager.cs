using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Contracts;

/// <summary>
/// Manages conversation sessions across channels for engagement mode
/// </summary>
public interface IConversationSessionManager
{
    /// <summary>
    /// Gets an active session for the specified channel, or null if none exists
    /// </summary>
    ConversationSession? GetActiveSession(ulong channelId);
    
    /// <summary>
    /// Starts a new session for the specified channel with the given bot
    /// </summary>
    ConversationSession StartSession(ulong channelId, IBotConfiguration bot);
    
    /// <summary>
    /// Gets or creates a session for the channel
    /// </summary>
    ConversationSession GetOrCreateSession(ulong channelId, IBotConfiguration bot);
    
    /// <summary>
    /// Adds a message to the session's history
    /// </summary>
    void AddMessage(ulong channelId, ChatMessageContent message);
    
    /// <summary>
    /// Ends a session explicitly
    /// </summary>
    void EndSession(ulong channelId);
    
    /// <summary>
    /// Checks if a session exists and is active
    /// </summary>
    bool HasActiveSession(ulong channelId);
    
    /// <summary>
    /// Cleans up expired sessions
    /// </summary>
    void CleanupExpiredSessions();
}
