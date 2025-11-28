using TheSexy6BotWorker.DTOs.Voice;

namespace TheSexy6BotWorker.Services.Voice;

/// <summary>
/// Manages voice conversation sessions across Discord servers.
/// Handles session lifecycle, Discord voice connections, and OpenAI Realtime API integration.
/// </summary>
public interface IVoiceSessionService
{
    /// <summary>
    /// Creates a new voice session and connects to the specified Discord voice channel.
    /// </summary>
    /// <param name="guildId">Discord server (guild) ID</param>
    /// <param name="channelId">Discord voice channel ID</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Created session state</returns>
    /// <exception cref="InvalidOperationException">If bot is already in a voice channel in this guild</exception>
    /// <exception cref="UnauthorizedAccessException">If bot lacks permissions to join the channel</exception>
    Task<VoiceSessionState> CreateSessionAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active session for the specified guild.
    /// </summary>
    /// <param name="guildId">Discord server (guild) ID</param>
    /// <returns>Active session state, or null if no session exists</returns>
    Task<VoiceSessionState?> GetSessionAsync(ulong guildId);

    /// <summary>
    /// Ends a voice session and cleans up all resources.
    /// Disconnects from Discord voice and OpenAI, releases audio streams.
    /// </summary>
    /// <param name="sessionId">Unique session identifier</param>
    /// <returns>Final session state with metrics</returns>
    Task<VoiceSessionState> EndSessionAsync(Guid sessionId);
}
