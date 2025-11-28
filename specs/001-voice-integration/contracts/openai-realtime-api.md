# OpenAI Realtime API WebSocket Contract

**Feature**: 001-voice-integration
**Date**: 2025-11-25
**Type**: WebSocket Protocol
**Endpoint**: `wss://api.openai.com/v1/realtime`

## Overview

This document defines the WebSocket communication contract with the OpenAI Realtime API. All messages are JSON-encoded with event-based structure.

---

## Connection

### Initialization

**WebSocket URL**:
```
wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01
```

**Required Headers**:
```
Authorization: Bearer {OPENAI_API_KEY}
openai-beta: realtime=v1
```

**Connection Flow**:
```
1. Client initiates WebSocket connection
2. Server sends session.created event
3. Client sends session.update event with configuration
4. Server sends session.updated event (confirms configuration)
5. Client can now send audio/create responses
```

---

## Event Structure

All events follow this base schema:

```json
{
  "type": "event.type",
  "event_id": "evt_xxxxx",
  "session_id": "sess_xxxxx",
  "payload": { /* event-specific data */ }
}
```

---

## Client → Server Events

### 1. session.update

**Purpose**: Configure session settings (voice, instructions, tools)

**When**: Immediately after connection established

**Payload**:
```json
{
  "type": "session.update",
  "session": {
    "modalities": ["text", "audio"],
    "instructions": "You are a helpful assistant in a Discord voice channel...",
    "voice": "alloy",
    "input_audio_format": "pcm16",
    "output_audio_format": "pcm16",
    "input_audio_transcription": {
      "model": "whisper-1"
    },
    "turn_detection": {
      "type": "server_vad",
      "threshold": 0.5,
      "prefix_padding_ms": 300,
      "silence_duration_ms": 500
    },
    "tools": [
      {
        "type": "function",
        "name": "get_weather",
        "description": "Get current weather for a city",
        "parameters": {
          "type": "object",
          "properties": {
            "city": {
              "type": "string",
              "description": "City name"
            }
          },
          "required": ["city"]
        }
      }
    ],
    "tool_choice": "auto",
    "temperature": 0.8,
    "max_response_output_tokens": 4096
  }
}
```

**Response**: `session.updated` event

---

### 2. input_audio_buffer.append

**Purpose**: Send audio data to the server

**When**: Continuously while user is speaking

**Payload**:
```json
{
  "type": "input_audio_buffer.append",
  "audio": "<base64-encoded-pcm16-audio>"
}
```

**Audio Format**:
- Sample Rate: 24,000 Hz
- Channels: 1 (mono)
- Encoding: PCM16 (16-bit signed integer, little-endian)
- Encoding for transmission: Base64

**Example** (C#):
```csharp
var audioBytes = new byte[960]; // 20ms of audio at 24kHz mono
var base64Audio = Convert.ToBase64String(audioBytes);

var message = new
{
    type = "input_audio_buffer.append",
    audio = base64Audio
};

await webSocket.SendAsync(JsonSerializer.Serialize(message));
```

---

### 3. input_audio_buffer.commit

**Purpose**: Signal end of user speech, trigger response generation

**When**: After user stops speaking (detected by client or timeout)

**Payload**:
```json
{
  "type": "input_audio_buffer.commit"
}
```

**Response**: Server begins generating response, sends `response.audio.delta` events

---

### 4. response.create

**Purpose**: Manually trigger AI response generation

**When**: After committing audio, or for text-only responses

**Payload**:
```json
{
  "type": "response.create",
  "response": {
    "modalities": ["text", "audio"],
    "instructions": "Respond concisely",
    "voice": "alloy",
    "output_audio_format": "pcm16",
    "temperature": 0.8
  }
}
```

**Optional**: Can override session-level settings per response

---

### 5. conversation.item.create

**Purpose**: Add items to conversation history (e.g., function results)

**When**: After executing a function call

**Payload** (Function Call Result):
```json
{
  "type": "conversation.item.create",
  "item": {
    "type": "function_call_output",
    "call_id": "call_xxxxx",
    "output": "{\"temperature\": 72, \"condition\": \"Sunny\"}"
  }
}
```

**Payload** (Manual Message):
```json
{
  "type": "conversation.item.create",
  "item": {
    "type": "message",
    "role": "user",
    "content": [
      {
        "type": "input_text",
        "text": "What's the weather like?"
      }
    ]
  }
}
```

---

### 6. response.cancel

**Purpose**: Stop an in-progress response

**When**: User interrupts the AI mid-response

**Payload**:
```json
{
  "type": "response.cancel"
}
```

---

## Server → Client Events

### 1. session.created

**Purpose**: Confirm WebSocket connection established

**When**: Immediately after client connects

**Payload**:
```json
{
  "type": "session.created",
  "event_id": "evt_xxxxx",
  "session": {
    "id": "sess_xxxxx",
    "object": "realtime.session",
    "model": "gpt-4o-realtime-preview-2024-10-01",
    "modalities": ["text", "audio"],
    "instructions": "",
    "voice": "alloy",
    "input_audio_format": "pcm16",
    "output_audio_format": "pcm16",
    "input_audio_transcription": null,
    "turn_detection": null,
    "tools": [],
    "tool_choice": "auto",
    "temperature": 0.8,
    "max_response_output_tokens": "inf"
  }
}
```

**Client Action**: Send `session.update` to configure session

---

### 2. session.updated

**Purpose**: Confirm session configuration applied

**When**: After client sends `session.update`

**Payload**:
```json
{
  "type": "session.updated",
  "event_id": "evt_xxxxx",
  "session": {
    /* Updated session object with new settings */
  }
}
```

**Client Action**: Begin sending audio or creating responses

---

### 3. input_audio_buffer.speech_started

**Purpose**: Server detected speech in audio buffer (Server VAD)

**When**: User begins speaking

**Payload**:
```json
{
  "type": "input_audio_buffer.speech_started",
  "event_id": "evt_xxxxx",
  "audio_start_ms": 1234,
  "item_id": "item_xxxxx"
}
```

**Client Action**: Stop any ongoing AI audio playback (user interrupt)

---

### 4. input_audio_buffer.speech_stopped

**Purpose**: Server detected end of speech (Server VAD)

**When**: User stops speaking (silence detected)

**Payload**:
```json
{
  "type": "input_audio_buffer.speech_stopped",
  "event_id": "evt_xxxxx",
  "audio_end_ms": 5678,
  "item_id": "item_xxxxx"
}
```

**Client Action**: Server will automatically commit audio and begin response

---

### 5. conversation.item.input_audio_transcription.completed

**Purpose**: Transcription of user speech available

**When**: After user speech is processed

**Payload**:
```json
{
  "type": "conversation.item.input_audio_transcription.completed",
  "event_id": "evt_xxxxx",
  "item_id": "item_xxxxx",
  "content_index": 0,
  "transcript": "What's the weather like in Seattle?"
}
```

**Client Action**: Log transcription, display to user (optional)

---

### 6. response.audio.delta

**Purpose**: AI audio response chunk

**When**: During response generation (streaming)

**Payload**:
```json
{
  "type": "response.audio.delta",
  "event_id": "evt_xxxxx",
  "response_id": "resp_xxxxx",
  "item_id": "item_xxxxx",
  "output_index": 0,
  "content_index": 0,
  "delta": "<base64-encoded-pcm16-audio>"
}
```

**Audio Format**: Same as input (24kHz, mono, PCM16, base64-encoded)

**Client Action**: Decode base64, resample to 48kHz stereo, play in Discord

**Example** (C#):
```csharp
var audioBytes = Convert.FromBase64String(payload.delta);
var frame = new AudioFrame(audioBytes, 24000, 1, 16);
var resampledFrame = audioConverter.ToDiscordFormat(frame);
await voiceTransmitSink.WriteAsync(resampledFrame.Data);
```

---

### 7. response.audio.done

**Purpose**: AI audio response complete

**When**: After all audio deltas sent

**Payload**:
```json
{
  "type": "response.audio.done",
  "event_id": "evt_xxxxx",
  "response_id": "resp_xxxxx",
  "item_id": "item_xxxxx",
  "output_index": 0,
  "content_index": 0
}
```

**Client Action**: Flush audio buffers, prepare for next user input

---

### 8. response.function_call_arguments.done

**Purpose**: AI wants to call a function

**When**: During response generation (if tool_choice allows)

**Payload**:
```json
{
  "type": "response.function_call_arguments.done",
  "event_id": "evt_xxxxx",
  "response_id": "resp_xxxxx",
  "item_id": "item_xxxxx",
  "output_index": 0,
  "call_id": "call_xxxxx",
  "name": "get_weather",
  "arguments": "{\"city\": \"Seattle\"}"
}
```

**Client Action**:
1. Parse `arguments` JSON
2. Execute function (e.g., `WeatherService.GetWeatherAsync("Seattle")`)
3. Send result via `conversation.item.create`
4. Trigger new response with `response.create`

---

### 9. error

**Purpose**: Error occurred

**When**: Any validation or processing error

**Payload**:
```json
{
  "type": "error",
  "event_id": "evt_xxxxx",
  "error": {
    "type": "invalid_request_error",
    "code": "invalid_value",
    "message": "Invalid audio format",
    "param": "input_audio_format"
  }
}
```

**Common Error Types**:
| Type | Code | Description |
|------|------|-------------|
| `invalid_request_error` | `invalid_value` | Bad parameter value |
| `invalid_request_error` | `missing_parameter` | Required param missing |
| `authentication_error` | `invalid_api_key` | API key invalid/expired |
| `rate_limit_error` | `rate_limit_exceeded` | Too many requests |
| `server_error` | `internal_error` | OpenAI server issue |

**Client Action**: Log error, attempt recovery or disconnect

---

## Event Flow Diagrams

### Typical Conversation Flow

```
[Connection Established]
Server → Client: session.created

Client → Server: session.update (configure tools, voice, VAD)
Server → Client: session.updated

[User speaks in Discord]
Client → Server: input_audio_buffer.append (continuous)
Server → Client: input_audio_buffer.speech_started

[User stops speaking]
Server → Client: input_audio_buffer.speech_stopped
Server → Client: conversation.item.input_audio_transcription.completed
                 (transcript: "What's the weather like?")

[AI generates response]
Server → Client: response.audio.delta (chunk 1)
Server → Client: response.audio.delta (chunk 2)
...
Server → Client: response.audio.delta (chunk N)
Server → Client: response.audio.done

[User hears AI response in Discord]
```

### Function Calling Flow

```
[User asks: "What's the weather in Seattle?"]
Client → Server: input_audio_buffer.append + commit

Server → Client: response.function_call_arguments.done
                 (name: "get_weather", arguments: {"city": "Seattle"})

[Client executes function]
var result = await weatherService.GetWeatherAsync("Seattle");
// result: {"temperature": 72, "condition": "Sunny"}

Client → Server: conversation.item.create
                 (type: function_call_output, call_id: "call_xxx", output: result)

Client → Server: response.create (trigger new response with function result)

Server → Client: response.audio.delta (AI says: "It's sunny and 72 degrees...")
Server → Client: response.audio.done
```

---

## Implementation Pattern (C#)

### WebSocket Message Handler

```csharp
private async Task HandleServerMessageAsync(OpenAIRealtimeMessage message)
{
    switch (message.Type)
    {
        case "session.created":
            _logger.LogInformation("Session created: {SessionId}", message.Payload.GetProperty("session").GetProperty("id"));
            await ConfigureSessionAsync();
            break;

        case "session.updated":
            _logger.LogInformation("Session updated successfully");
            OnSessionReady?.Invoke();
            break;

        case "input_audio_buffer.speech_started":
            _logger.LogDebug("User started speaking");
            await StopPlaybackAsync(); // Interrupt AI if speaking
            break;

        case "input_audio_buffer.speech_stopped":
            _logger.LogDebug("User stopped speaking");
            // Server auto-commits, no action needed
            break;

        case "conversation.item.input_audio_transcription.completed":
            var transcript = message.Payload.GetProperty("transcript").GetString();
            _logger.LogInformation("User said: {Transcript}", transcript);
            await LogConversationMessageAsync(MessageRole.User, transcript);
            break;

        case "response.audio.delta":
            var audioBase64 = message.Payload.GetProperty("delta").GetString();
            var audioBytes = Convert.FromBase64String(audioBase64);
            await ProcessAudioChunkAsync(audioBytes);
            break;

        case "response.audio.done":
            _logger.LogInformation("Response audio complete");
            await FlushAudioBufferAsync();
            break;

        case "response.function_call_arguments.done":
            var callId = message.Payload.GetProperty("call_id").GetString();
            var functionName = message.Payload.GetProperty("name").GetString();
            var argsJson = message.Payload.GetProperty("arguments").GetString();
            await ExecuteFunctionAsync(callId, functionName, argsJson);
            break;

        case "error":
            var error = message.Payload.GetProperty("error");
            _logger.LogError("OpenAI error: {Code} - {Message}",
                error.GetProperty("code").GetString(),
                error.GetProperty("message").GetString());
            await HandleErrorAsync(error);
            break;

        default:
            _logger.LogDebug("Unhandled event type: {Type}", message.Type);
            break;
    }
}
```

---

## Rate Limits

| Tier | RPM | RPD | TPM | Concurrent Sessions |
|------|-----|-----|-----|---------------------|
| 1 | 100 | 10,000 | 20,000 | ~10 |
| 2 | 500 | 50,000 | 50,000 | ~25 |
| 3 | 1,000 | 100,000 | 100,000 | ~50 |
| 4 | 5,000 | 500,000 | 200,000 | ~75 |
| 5 | 10,000 | 1,000,000 | 2,000,000 | ~100 |

**Notes**:
- RPM: Requests Per Minute
- RPD: Requests Per Day
- TPM: Tokens Per Minute
- Concurrent Sessions: Approximate, not hard limit

---

## Error Handling Strategy

```csharp
public class OpenAIRealtimeClient
{
    private int _reconnectAttempts = 0;
    private const int MAX_RECONNECT_ATTEMPTS = 5;

    private async Task HandleConnectionErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "WebSocket connection error");

        if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
        {
            _logger.LogCritical("Max reconnection attempts reached. Giving up.");
            OnFatalError?.Invoke(ex);
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Pow(2, _reconnectAttempts)); // Exponential backoff
        _logger.LogWarning("Reconnecting in {Delay}s (attempt {Attempt}/{Max})",
            delay.TotalSeconds, _reconnectAttempts + 1, MAX_RECONNECT_ATTEMPTS);

        await Task.Delay(delay);
        _reconnectAttempts++;

        await ReconnectAsync();
    }

    private async Task ReconnectAsync()
    {
        try
        {
            await ConnectAsync(_sessionId);
            _reconnectAttempts = 0; // Reset on successful reconnection
            _logger.LogInformation("Reconnection successful");
        }
        catch (Exception ex)
        {
            await HandleConnectionErrorAsync(ex);
        }
    }
}
```

---

## Testing Requirements

### Unit Tests
- Event serialization/deserialization
- Message validation
- Error parsing

### Integration Tests
- Full connection lifecycle (connect, configure, disconnect)
- Audio streaming (send/receive)
- Function calling flow
- Error scenarios (invalid auth, network timeout)

### Manual Tests
- Real-time voice conversation with interruptions
- Function calling with actual Semantic Kernel plugins
- Network instability scenarios

---

**Contract Status**: Complete ✓
**Next Step**: Create quickstart documentation for setup and development
