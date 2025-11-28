namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Represents the current state of a voice session.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Session is being created, establishing Discord and OpenAI connections.
    /// </summary>
    Initializing,

    /// <summary>
    /// WebSockets connected to both Discord and OpenAI, waiting for audio.
    /// </summary>
    Connected,

    /// <summary>
    /// Actively processing audio and conversation exchanges.
    /// </summary>
    Active,

    /// <summary>
    /// Cleanup in progress, disconnecting from services.
    /// </summary>
    Disconnecting,

    /// <summary>
    /// Session successfully closed and all resources released.
    /// </summary>
    Completed,

    /// <summary>
    /// Error state encountered, cleanup required.
    /// </summary>
    Error
}
