using TheSexy6BotWorker.DTOs.Voice;

namespace TheSexy6BotWorker.Services.Voice;

/// <summary>
/// Client for OpenAI Realtime API WebSocket connections.
/// Handles bidirectional audio streaming and conversation management.
/// </summary>
public interface IOpenAIRealtimeClient : IDisposable
{
    /// <summary>
    /// Establishes a WebSocket connection to OpenAI Realtime API.
    /// </summary>
    /// <param name="sessionConfig">Configuration for the voice session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConnectAsync(VoiceSessionConfig sessionConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends PCM audio data to OpenAI for processing.
    /// Audio must be in 24kHz 16-bit mono PCM format.
    /// </summary>
    /// <param name="audioData">PCM audio bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a text message to the conversation.
    /// </summary>
    /// <param name="message">Text message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendTextAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when audio response is received from OpenAI.
    /// Audio is in 24kHz 16-bit mono PCM format.
    /// </summary>
    event EventHandler<byte[]>? AudioReceived;

    /// <summary>
    /// Event raised when a text transcript is received from OpenAI.
    /// </summary>
    event EventHandler<string>? TranscriptReceived;

    /// <summary>
    /// Event raised when an error occurs in the WebSocket connection.
    /// </summary>
    event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Closes the WebSocket connection and cleans up resources.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the client is currently connected to OpenAI.
    /// </summary>
    bool IsConnected { get; }
}
