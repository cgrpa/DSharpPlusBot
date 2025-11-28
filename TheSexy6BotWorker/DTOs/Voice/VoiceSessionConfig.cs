using System.ComponentModel.DataAnnotations;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Configuration and limits for a voice session.
/// </summary>
public class VoiceSessionConfig
{
    /// <summary>
    /// Maximum session length in minutes.
    /// </summary>
    [Required]
    [Range(1, 30)]
    public int MaxSessionDurationMinutes { get; init; } = 10;

    /// <summary>
    /// Timeout for inactivity in seconds (auto-disconnect after silence).
    /// </summary>
    [Required]
    [Range(30, 600)]
    public int AutoDisconnectOnSilenceSeconds { get; init; } = 300; // 5 minutes

    /// <summary>
    /// Allow AI to call Semantic Kernel plugins via voice.
    /// </summary>
    [Required]
    public bool EnableFunctionCalling { get; init; } = true;

    /// <summary>
    /// OpenAI voice model name.
    /// Options: alloy, echo, fable, onyx, nova, shimmer
    /// </summary>
    [Required]
    public string VoiceModel { get; init; } = "alloy";

    /// <summary>
    /// AI response randomness (0.0 = deterministic, 1.0 = very creative).
    /// </summary>
    [Required]
    [Range(0.0f, 1.0f)]
    public float Temperature { get; init; } = 0.8f;

    /// <summary>
    /// Maximum number of messages to retain in conversation history.
    /// </summary>
    [Required]
    [Range(1, 50)]
    public int MaxContextMessages { get; init; } = 20;

    /// <summary>
    /// Maximum cost for this session in cents (null = unlimited).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? CostLimitCents { get; init; }
}
