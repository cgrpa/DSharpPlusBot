using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// WebSocket message schema for OpenAI Realtime API.
/// All events follow this base structure with event-specific payloads.
/// </summary>
public class OpenAIRealtimeMessage
{
    /// <summary>
    /// Event type (e.g., "session.update", "input_audio_buffer.append", "response.audio.delta").
    /// See OpenAI Realtime API documentation for full list of event types.
    /// </summary>
    [Required]
    public required string Type { get; init; }

    /// <summary>
    /// Unique event identifier (server-generated for server→client events).
    /// Optional for client→server events.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// Event-specific data (structure varies by Type).
    /// Use JsonElement for flexible deserialization based on event type.
    /// </summary>
    [Required]
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// OpenAI session identifier (required after session.created event).
    /// </summary>
    public string? SessionId { get; init; }
}
