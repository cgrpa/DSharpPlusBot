using System.ComponentModel.DataAnnotations;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// A single message in the voice conversation history.
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Unique message identifier (format: msg_{timestamp}_{sequence}).
    /// </summary>
    [Required]
    public required string MessageId { get; init; }

    /// <summary>
    /// Who sent the message (System, User, Assistant, or Function).
    /// </summary>
    [Required]
    public required MessageRole Role { get; init; }

    /// <summary>
    /// Message text content (optional for function calls, required for text messages).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Base64-decoded audio data (only for User role with speech input).
    /// </summary>
    public byte[]? AudioData { get; init; }

    /// <summary>
    /// Function call details (only for Assistant role when calling a function).
    /// </summary>
    public FunctionCallData? FunctionCall { get; init; }

    /// <summary>
    /// Function execution result (only for Function role).
    /// </summary>
    public string? FunctionResult { get; init; }

    /// <summary>
    /// When the message was created (UTC timestamp).
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Estimated token usage for cost tracking.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int TokenCount { get; init; }
}
