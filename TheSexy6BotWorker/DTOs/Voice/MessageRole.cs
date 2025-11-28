namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Represents the role of a message sender in a voice conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// System instructions or configuration messages.
    /// </summary>
    System,

    /// <summary>
    /// User speech input from Discord voice channel.
    /// </summary>
    User,

    /// <summary>
    /// AI-generated voice output from OpenAI Realtime API.
    /// </summary>
    Assistant,

    /// <summary>
    /// Function call result returned from Semantic Kernel plugin.
    /// </summary>
    Function
}
