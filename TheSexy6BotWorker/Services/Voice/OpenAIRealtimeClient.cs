using Ardalis.GuardClauses;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheSexy6BotWorker.DTOs.Voice;

namespace TheSexy6BotWorker.Services.Voice;

/// <summary>
/// Client for OpenAI Realtime API WebSocket connections.
/// Manages bidirectional audio streaming and conversation with OpenAI's Realtime API.
/// Based on https://platform.openai.com/docs/api-reference/realtime
/// </summary>
public class OpenAIRealtimeClient : IOpenAIRealtimeClient
{
    private readonly ILogger<OpenAIRealtimeClient> _logger;
    private readonly string _apiKey;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receivingCts;
    private Task? _receivingTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TranscriptReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public OpenAIRealtimeClient(ILogger<OpenAIRealtimeClient> logger, string apiKey)
    {
        _logger = Guard.Against.Null(logger);
        _apiKey = Guard.Against.NullOrEmpty(apiKey);
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(VoiceSessionConfig sessionConfig, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(sessionConfig);

        if (IsConnected)
        {
            throw new InvalidOperationException("Already connected to OpenAI Realtime API");
        }

        try
        {
            _logger.LogInformation("Connecting to OpenAI Realtime API");

            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            var uri = new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17");
            await _webSocket.ConnectAsync(uri, cancellationToken);

            // Send session configuration
            await SendSessionUpdateAsync(sessionConfig, cancellationToken);

            // Start receiving responses in background
            _receivingCts = new CancellationTokenSource();
            _receivingTask = ReceiveMessagesAsync(_receivingCts.Token);

            _logger.LogInformation("Successfully connected to OpenAI Realtime API");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OpenAI Realtime API");
            await CleanupAsync();
            throw;
        }
    }

    private async Task SendSessionUpdateAsync(VoiceSessionConfig config, CancellationToken cancellationToken)
    {
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = "You are a helpful AI assistant in a Discord voice channel. " +
                              "Respond naturally and conversationally. Keep responses concise and engaging.",
                voice = config.VoiceModel ?? "alloy",
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                input_audio_transcription = new
                {
                    model = "whisper-1"
                },
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500
                },
                temperature = config.Temperature,
                max_response_output_tokens = config.MaxContextMessages * 100
            }
        };

        await SendJsonAsync(sessionUpdate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(audioData);

        if (!IsConnected || _webSocket == null)
        {
            throw new InvalidOperationException("Not connected to OpenAI Realtime API");
        }

        try
        {
            var base64Audio = Convert.ToBase64String(audioData);
            var message = new
            {
                type = "input_audio_buffer.append",
                audio = base64Audio
            };

            await SendJsonAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audio to OpenAI");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SendTextAsync(string message, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(message);

        if (!IsConnected || _webSocket == null)
        {
            throw new InvalidOperationException("Not connected to OpenAI Realtime API");
        }

        try
        {
            var conversationItem = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = new[]
                    {
                        new { type = "input_text", text = message }
                    }
                }
            };

            await SendJsonAsync(conversationItem, cancellationToken);

            // Trigger response
            var responseCreate = new { type = "response.create" };
            await SendJsonAsync(responseCreate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text to OpenAI");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from OpenAI Realtime API");
        await CleanupAsync();
    }

    private async Task SendJsonAsync(object data, CancellationToken cancellationToken)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);

        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

        _logger.LogTrace("Sent to OpenAI: {Json}", json);
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null)
        {
            return;
        }

        var buffer = new byte[1024 * 16]; // 16KB buffer
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket closed by server");
                    break;
                }

                var messageChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(messageChunk);

                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();

                    await ProcessMessageAsync(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped receiving messages from OpenAI (cancelled)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from OpenAI");
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private Task ProcessMessageAsync(string json)
    {
        try
        {
            _logger.LogTrace("Received from OpenAI: {Json}", json);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return Task.CompletedTask;
            }

            var messageType = typeElement.GetString();

            switch (messageType)
            {
                case "response.audio.delta":
                    if (root.TryGetProperty("delta", out var deltaElement))
                    {
                        var base64Audio = deltaElement.GetString();
                        if (!string.IsNullOrEmpty(base64Audio))
                        {
                            var audioBytes = Convert.FromBase64String(base64Audio);
                            AudioReceived?.Invoke(this, audioBytes);
                        }
                    }
                    break;

                case "response.audio_transcript.delta":
                    if (root.TryGetProperty("delta", out var transcriptElement))
                    {
                        var transcript = transcriptElement.GetString();
                        if (!string.IsNullOrEmpty(transcript))
                        {
                            TranscriptReceived?.Invoke(this, transcript);
                        }
                    }
                    break;

                case "conversation.item.input_audio_transcription.completed":
                    if (root.TryGetProperty("transcript", out var userTranscriptElement))
                    {
                        var userTranscript = userTranscriptElement.GetString();
                        _logger.LogDebug("User speech transcribed: {Transcript}", userTranscript);
                    }
                    break;

                case "error":
                    if (root.TryGetProperty("error", out var errorElement))
                    {
                        var errorMessage = errorElement.TryGetProperty("message", out var msgElement)
                            ? msgElement.GetString()
                            : "Unknown error";
                        var error = new Exception($"OpenAI Realtime API error: {errorMessage}");
                        _logger.LogError(error, "Received error from OpenAI");
                        ErrorOccurred?.Invoke(this, error);
                    }
                    break;

                case "session.created":
                case "session.updated":
                    _logger.LogDebug("Session event: {Type}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from OpenAI");
        }

        return Task.CompletedTask;
    }

    private async Task CleanupAsync()
    {
        // Cancel receiving task
        _receivingCts?.Cancel();
        if (_receivingTask != null)
        {
            try
            {
                await _receivingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        // Close WebSocket
        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing connection",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing WebSocket");
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }

        _receivingCts?.Dispose();
        _receivingCts = null;
        _receivingTask = null;

        _logger.LogInformation("OpenAI Realtime API client cleaned up");
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
