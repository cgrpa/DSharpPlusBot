using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace TheSexy6BotWorker.Services
{
    /// <summary>
    /// Manages OpenAI Realtime API WebSocket connections for voice conversations
    /// </summary>
    public class RealtimeService : IAsyncDisposable
    {
        private readonly ILogger<RealtimeService> _logger;
        private readonly string _apiKey;
        private readonly AudioConverter _audioConverter;
        private readonly Kernel _kernel;
        private readonly ConcurrentDictionary<string, WebSocketSession> _activeSessions = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastActivityTime = new();

        public RealtimeService(
            IConfiguration configuration,
            ILogger<RealtimeService> logger,
            AudioConverter audioConverter,
            Kernel kernel)
        {
            _logger = logger;
            _audioConverter = audioConverter;
            _kernel = kernel;
            _apiKey = configuration["OpenAIApiKey"]
                ?? throw new InvalidOperationException("OpenAIApiKey not configured in app settings or user secrets");
        }

        /// <summary>
        /// Starts a new realtime conversation session
        /// </summary>
        /// <param name="sessionId">Unique identifier for this session (e.g., Discord guild ID)</param>
        /// <param name="onAudioReceived">Callback when AI speaks (receives 24kHz mono PCM)</param>
        /// <param name="onError">Callback for error handling</param>
        public async Task<bool> StartSessionAsync(
            string sessionId,
            Func<byte[], Task> onAudioReceived,
            Func<Exception, Task>? onError = null)
        {
            try
            {
                if (_activeSessions.ContainsKey(sessionId))
                {
                    _logger.LogWarning("Session {SessionId} already exists", sessionId);
                    return false;
                }

                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                client.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

                _logger.LogInformation("Opening OpenAI Realtime connection for session {SessionId}", sessionId);

                await client.ConnectAsync(
                    new Uri("wss://api.openai.com/v1/realtime?model=gpt-realtime-mini"),
                    CancellationToken.None);

                _logger.LogInformation("OpenAI Realtime WebSocket connected for session {SessionId}", sessionId);

                var session = new WebSocketSession
                {
                    Client = client,
                    OnAudioReceived = onAudioReceived,
                    OnError = onError,
                    CancellationTokenSource = new CancellationTokenSource()
                };

                _activeSessions.TryAdd(sessionId, session);
                _lastActivityTime[sessionId] = DateTime.UtcNow;

                // Start receiving messages
                _logger.LogDebug("Session {SessionId}: starting background receive loop", sessionId);
                _ = Task.Run(async () => await ReceiveMessagesAsync(sessionId, session));

                // Wait for session.created event and send session.update
                await Task.Delay(500); // Give time for connection to establish
                await SendSessionUpdateAsync(sessionId);

                _logger.LogInformation("Started Realtime API session {SessionId}", sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Realtime API session {SessionId}", sessionId);
                onError?.Invoke(ex);
                return false;
            }
        }

        /// <summary>
        /// Sends audio from Discord user to OpenAI (already converted to 24kHz mono)
        /// </summary>
        public async Task SendAudioAsync(string sessionId, byte[] audioData)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Session {SessionId} not found for audio send", sessionId);
                return;
            }

            try
            {
                var base64Audio = Convert.ToBase64String(audioData);
                _logger.LogTrace(
                    "Session {SessionId}: base64 audio payload length={Length}",
                    sessionId,
                    base64Audio.Length);
                var audioMessage = new JObject
                {
                    ["type"] = "input_audio_buffer.append",
                    ["audio"] = base64Audio
                };

                _logger.LogDebug(
                    "Session {SessionId}: appending {ByteCount} bytes of audio to OpenAI buffer",
                    sessionId,
                    audioData.Length);

                await SendMessageAsync(session.Client, audioMessage);
                _lastActivityTime[sessionId] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send audio to session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Stops the realtime conversation session
        /// </summary>
        public async Task StopSessionAsync(string sessionId)
        {
            if (_activeSessions.TryRemove(sessionId, out var session))
            {
                try
                {
                    session.CancellationTokenSource.Cancel();

                    if (session.Client.State == WebSocketState.Open)
                    {
                        await session.Client.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing connection",
                            CancellationToken.None);
                    }

                    session.Client.Dispose();
                    _lastActivityTime.TryRemove(sessionId, out _);
                    _logger.LogInformation("Stopped Realtime API session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping session {SessionId}", sessionId);
                }
            }
        }

        /// <summary>
        /// Checks if a session has been inactive for more than the specified threshold
        /// </summary>
        public bool IsSessionInactive(string sessionId, int inactivityThresholdSeconds = 30)
        {
            if (!_lastActivityTime.TryGetValue(sessionId, out var lastActivity))
                return true;

            return (DateTime.UtcNow - lastActivity).TotalSeconds > inactivityThresholdSeconds;
        }

        /// <summary>
        /// Sends session configuration with tools from Semantic Kernel
        /// </summary>
        private async Task SendSessionUpdateAsync(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return;

            // Minimal realtime session configuration: no tools, just audio + text
            var sessionConfig = new JObject
            {
                ["type"] = "session.update",
                ["session"] = new JObject
                {
                    ["instructions"] = "You are a helpful AI in a Discord voice chat. Keep responses short and conversational.",
                    ["voice"] = "alloy",
                    ["temperature"] = 1,
                    ["max_response_output_tokens"] = 4096,
                    ["modalities"] = new JArray("text", "audio"),
                    ["input_audio_format"] = "pcm16",
                    ["output_audio_format"] = "pcm16",
                    ["input_audio_transcription"] = new JObject { ["model"] = "whisper-1" }
                }
            };

            await SendMessageAsync(session.Client, sessionConfig);
            _logger.LogInformation("Sent minimal session update for {SessionId} (no tools)", sessionId);
        }

        /// <summary>
        /// Receives and processes messages from the WebSocket
        /// </summary>
        private async Task ReceiveMessagesAsync(string sessionId, WebSocketSession session)
        {
            var buffer = new byte[1024 * 16];
            var messageBuffer = new StringBuilder();

            try
            {
                while (session.Client.State == WebSocketState.Open &&
                       !session.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await session.Client.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        session.CancellationTokenSource.Token);

                    _logger.LogTrace(
                        "Session {SessionId}: received WebSocket chunk bytes={ByteCount}, endOfMessage={EndOfMessage}, type={MessageType}",
                        sessionId,
                        result.Count,
                        result.EndOfMessage,
                        result.MessageType);

                    var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuffer.Append(chunk);

                    if (result.EndOfMessage)
                    {
                        var jsonResponse = messageBuffer.ToString();
                        messageBuffer.Clear();

                        _logger.LogDebug(
                            "Session {SessionId}: received full WebSocket message (length={Length})",
                            sessionId,
                            jsonResponse.Length);

                        if (jsonResponse.Trim().StartsWith("{"))
                        {
                            var json = JObject.Parse(jsonResponse);
                            _logger.LogTrace(
                                "Session {SessionId}: dispatching event {EventType}",
                                sessionId,
                                json["type"]?.ToString());
                            await HandleWebSocketMessageAsync(sessionId, session, json);
                        }
                    }

                    _lastActivityTime[sessionId] = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving messages for session {SessionId}", sessionId);
                session.OnError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Handles individual WebSocket messages
        /// </summary>
        private async Task HandleWebSocketMessageAsync(string sessionId, WebSocketSession session, JObject json)
        {
            var type = json["type"]?.ToString();

            switch (type)
            {
                case "session.created":
                    _logger.LogDebug("Session created for {SessionId}", sessionId);
                    break;

                case "session.updated":
                    _logger.LogDebug("Session updated for {SessionId}", sessionId);
                    break;

                case "response.created":
                    _logger.LogDebug(
                        "Session {SessionId}: response created (responseId={ResponseId})",
                        sessionId,
                        json["response"]?["id"]?.ToString());
                    break;

                case "response.completed":
                    _logger.LogDebug(
                        "Session {SessionId}: response complete (responseId={ResponseId})",
                        sessionId,
                        json["response"]?["id"]?.ToString());
                    break;

                case "response.audio.delta":
                    var base64Audio = json["delta"]?.ToString();
                    if (!string.IsNullOrEmpty(base64Audio))
                    {
                        var audioBytes = Convert.FromBase64String(base64Audio);
                        _logger.LogDebug(
                            "Session {SessionId}: received {ByteCount} bytes of AI audio delta",
                            sessionId,
                            audioBytes.Length);
                        await session.OnAudioReceived(audioBytes);
                    }
                    break;

                case "response.output_text.delta":
                case "response.text.delta":
                    var textDelta = json["delta"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(textDelta))
                    {
                        _logger.LogDebug(
                            "Session {SessionId}: text delta => {Text}",
                            sessionId,
                            textDelta);
                    }
                    break;

                case "conversation.item.created":
                    _logger.LogDebug(
                        "Session {SessionId}: conversation item created (itemType={ItemType})",
                        sessionId,
                        json["item"]?["type"]?.ToString());
                    break;

                // Tools are disabled for now; ignore any function call events if received
                case "response.function_call_arguments.done":
                    _logger.LogDebug("Session {SessionId}: function-call event ignored (tools disabled)", sessionId);
                    break;

                case "error":
                    var errorMessage = json["error"]?["message"]?.ToString() ?? "Unknown error";
                    _logger.LogError("Realtime API error for session {SessionId}: {Error}", sessionId, errorMessage);
                    session.OnError?.Invoke(new Exception(errorMessage));
                    break;

                default:
                    _logger.LogTrace(
                        "Unhandled event type for session {SessionId}: {Type} Payload={Payload}",
                        sessionId,
                        type,
                        json.ToString(Formatting.None));
                    break;
            }
        }

        /// <summary>
        /// Handles function calls from the Realtime API
        /// </summary>
        private async Task HandleFunctionCallAsync(string sessionId, WebSocketSession session, JObject json)
        {
            try
            {
                var functionName = json["name"]?.ToString();
                var callId = json["call_id"]?.ToString();
                var arguments = json["arguments"]?.ToString();

                if (string.IsNullOrEmpty(functionName) || string.IsNullOrEmpty(callId))
                    return;

                _logger.LogInformation("Function call for session {SessionId}: {FunctionName}", sessionId, functionName);
                if (!string.IsNullOrEmpty(arguments))
                {
                    _logger.LogDebug(
                        "Session {SessionId}: function {FunctionName} arguments => {Arguments}",
                        sessionId,
                        functionName,
                        arguments);
                }

                // Find and invoke the function through Semantic Kernel
                KernelFunction? targetFunction = null;
                foreach (var plugin in _kernel.Plugins)
                {
                    targetFunction = plugin.FirstOrDefault(f => f.Metadata.Name == functionName);
                    if (targetFunction != null)
                        break;
                }

                string result;
                if (targetFunction != null && !string.IsNullOrEmpty(arguments))
                {
                    var argsDict = JObject.Parse(arguments);
                    var kernelArgs = new KernelArguments();
                    foreach (var kvp in argsDict)
                    {
                        kernelArgs[kvp.Key] = kvp.Value?.ToString();
                    }

                    var functionResult = await _kernel.InvokeAsync(targetFunction, kernelArgs);
                    result = functionResult.ToString() ?? string.Empty;
                }
                else
                {
                    result = $"Function '{functionName}' not found";
                }

                _logger.LogDebug(
                    "Session {SessionId}: function {FunctionName} result => {Result}",
                    sessionId,
                    functionName,
                    result);

                // Send function call result
                var resultJson = new JObject
                {
                    ["type"] = "conversation.item.create",
                    ["item"] = new JObject
                    {
                        ["type"] = "function_call_output",
                        ["output"] = result,
                        ["call_id"] = callId
                    }
                };

                await SendMessageAsync(session.Client, resultJson);

                // Trigger response
                var responseJson = new JObject
                {
                    ["type"] = "response.create"
                };

                await SendMessageAsync(session.Client, responseJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling function call for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Builds JSON schema for function parameters
        /// </summary>
        private string BuildParametersSchema(IReadOnlyList<KernelParameterMetadata> parameters)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(),
                ["required"] = new JArray()
            };

            var properties = (JObject)schema["properties"]!;
            var required = (JArray)schema["required"]!;

            foreach (var param in parameters)
            {
                var paramSchema = new JObject
                {
                    ["type"] = MapTypeToJsonSchemaType(param.ParameterType),
                    ["description"] = param.Description ?? string.Empty
                };

                properties[param.Name] = paramSchema;

                if (param.IsRequired)
                {
                    required.Add(param.Name);
                }
            }

            return schema.ToString();
        }

        /// <summary>
        /// Maps .NET types to JSON Schema types
        /// </summary>
        private string MapTypeToJsonSchemaType(Type? type)
        {
            if (type == null) return "string";
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(float) || type == typeof(double)) return "number";
            if (type == typeof(bool)) return "boolean";
            return "string";
        }

        /// <summary>
        /// Sends a JSON message to the WebSocket
        /// </summary>
        private async Task SendMessageAsync(ClientWebSocket client, JObject message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message.ToString());
            await client.SendAsync(
                new ArraySegment<byte>(messageBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var sessionId in _activeSessions.Keys.ToList())
            {
                await StopSessionAsync(sessionId);
            }
        }
    }

    /// <summary>
    /// Represents an active WebSocket session
    /// </summary>
    internal class WebSocketSession
    {
        public required ClientWebSocket Client { get; init; }
        public required Func<byte[], Task> OnAudioReceived { get; init; }
        public Func<Exception, Task>? OnError { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
    }
}
