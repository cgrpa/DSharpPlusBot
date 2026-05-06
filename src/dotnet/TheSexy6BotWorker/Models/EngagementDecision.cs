using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.Models;

/// <summary>
/// Structured output for engagement mode decisions.
/// The bot must return this format when deciding whether to respond to a message.
/// </summary>
public sealed class EngagementDecision
{
    /// <summary>
    /// Whether the bot should send a response to the channel
    /// </summary>
    [JsonPropertyName("shouldRespond")]
    public bool ShouldRespond { get; set; }

    /// <summary>
    /// The message to send if ShouldRespond is true. Can be null/empty if not responding.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
