using System.ComponentModel.DataAnnotations;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Container for audio data with format metadata.
/// Supports Discord (48kHz stereo) and OpenAI (24kHz mono) formats.
/// </summary>
public class AudioFrame
{
    /// <summary>
    /// Raw PCM audio samples (16-bit signed integer, little-endian).
    /// </summary>
    [Required]
    public required byte[] Data { get; init; }

    /// <summary>
    /// Samples per second (24000 for OpenAI, 48000 for Discord).
    /// </summary>
    [Required]
    [Range(24000, 48000)]
    public required int SampleRate { get; init; }

    /// <summary>
    /// Number of audio channels (1 for mono/OpenAI, 2 for stereo/Discord).
    /// </summary>
    [Required]
    [Range(1, 2)]
    public required int Channels { get; init; }

    /// <summary>
    /// Bit depth of audio samples (always 16 for PCM S16LE).
    /// </summary>
    [Required]
    public int BitsPerSample { get; init; } = 16;

    /// <summary>
    /// Duration of audio frame in milliseconds.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public required int DurationMs { get; init; }

    /// <summary>
    /// Timestamp when the frame was captured or generated.
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Validates that Data.Length matches the expected size based on format parameters.
    /// Expected size = (SampleRate * Channels * (BitsPerSample/8) * DurationMs) / 1000
    /// </summary>
    public bool IsValid()
    {
        var expectedSize = (SampleRate * Channels * (BitsPerSample / 8) * DurationMs) / 1000;
        return Data.Length == expectedSize;
    }
}
