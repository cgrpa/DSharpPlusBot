using System.ComponentModel.DataAnnotations;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Represents an active voice conversation session in a Discord channel.
/// </summary>
public class VoiceSessionState
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    [Required]
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Discord server (guild) ID where the session is active.
    /// </summary>
    [Required]
    [Range(1, ulong.MaxValue)]
    public required ulong GuildId { get; init; }

    /// <summary>
    /// Discord voice channel ID where the bot is connected.
    /// </summary>
    [Required]
    [Range(1, ulong.MaxValue)]
    public required ulong ChannelId { get; init; }

    /// <summary>
    /// When the session was created (UTC timestamp).
    /// </summary>
    [Required]
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last user interaction timestamp (updated on each voice exchange).
    /// Used for auto-disconnect timeout calculations.
    /// </summary>
    [Required]
    public DateTimeOffset LastActivityTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Current session state (Initializing, Connected, Active, etc.).
    /// </summary>
    [Required]
    public SessionState State { get; set; } = SessionState.Initializing;

    /// <summary>
    /// Number of users currently in the voice channel.
    /// Session auto-disconnects when this reaches 0.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ParticipantCount { get; set; }

    /// <summary>
    /// Conversation message history (max 50 messages per VoiceSessionConfig).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public List<ConversationMessage> ConversationContext { get; init; } = new();

    /// <summary>
    /// Session configuration (timeouts, cost limits, voice model, etc.).
    /// </summary>
    [Required]
    public required VoiceSessionConfig Configuration { get; init; }
}
