# Data Model: Discord Voice Integration

**Feature**: 001-voice-integration
**Date**: 2025-11-25
**Input**: Feature specification and research findings

## Overview

This document defines the data structures for voice integration, mapping functional requirements to strongly-typed C# entities. All entities follow DTO pattern (data only, no behavior) as required by the constitution.

---

## Core Entities

### 1. VoiceSessionState

**Purpose**: Represents an active voice conversation session in a Discord channel

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| SessionId | Guid | Unique identifier for the session | Required, generated on creation |
| GuildId | ulong | Discord server (guild) ID | Required, must be valid Discord snowflake |
| ChannelId | ulong | Discord voice channel ID | Required, must be valid Discord snowflake |
| StartTime | DateTimeOffset | When the session was created | Required, UTC timestamp |
| LastActivityTime | DateTimeOffset | Last user interaction timestamp | Required, UTC timestamp |
| State | SessionState (enum) | Current session state | Required, see SessionState enum |
| ParticipantCount | int | Number of users in the voice channel | >= 0 |
| ConversationContext | List<ConversationMessage> | Message history | Not null, max 50 messages |
| Configuration | VoiceSessionConfig | Session settings | Required |

**State Transitions**:
```
Initializing → Connected → Active → Disconnecting → Completed
             ↓          ↓           ↓
             → Error ←──────────────┘
```

**Relationships**:
- One-to-one with Discord voice channel (ChannelId)
- One-to-many with ConversationMessage (ConversationContext)
- One-to-one with VoiceSessionConfig (Configuration)

**Validation Rules**:
- SessionId must be unique across all sessions
- ChannelId must not have an existing active session (one bot per channel)
- LastActivityTime must be >= StartTime
- State transitions must follow valid paths (enforced by state machine)

---

### 2. VoiceSessionConfig

**Purpose**: Configuration and limits for a voice session

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| MaxSessionDurationMinutes | int | Maximum session length | 1-30, default: 10 |
| AutoDisconnectOnSilenceSeconds | int | Timeout for inactivity | 30-600, default: 300 (5 min) |
| EnableFunctionCalling | bool | Allow AI to call Semantic Kernel plugins | Default: true |
| VoiceModel | string | OpenAI voice model name | Default: "alloy", options: alloy/echo/fable/onyx/nova/shimmer |
| Temperature | float | AI response randomness | 0.0-1.0, default: 0.8 |
| MaxContextMessages | int | Conversation history limit | 1-50, default: 20 |
| CostLimitCents | int? | Max cost for this session (cents) | >= 0 or null (unlimited) |

**Validation Rules**:
- All numeric values must be within specified ranges
- VoiceModel must be one of supported voices
- Temperature must be between 0 and 1 inclusive

---

### 3. ConversationMessage

**Purpose**: A single message in the voice conversation history

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| MessageId | string | Unique message identifier | Required, format: msg_<timestamp>_<seq> |
| Role | MessageRole (enum) | Who sent the message | Required, see MessageRole enum |
| Content | string | Message text content | Optional (null for function calls) |
| AudioData | byte[]? | Base64-decoded audio data | Optional, used for user speech |
| FunctionCall | FunctionCallData? | Function call details | Optional, for assistant function calls |
| FunctionResult | string? | Function execution result | Optional, for function returns |
| Timestamp | DateTimeOffset | When message was created | Required, UTC timestamp |
| TokenCount | int | Estimated token usage | >= 0, estimated for cost tracking |

**Validation Rules**:
- MessageId must be unique within a session
- Content XOR FunctionCall must be set (not both null, not both set)
- FunctionResult only valid if Role is Function
- AudioData only valid if Role is User

---

### 4. AudioFrame

**Purpose**: Container for audio data with format metadata

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| Data | byte[] | Raw PCM audio samples | Required, not empty |
| SampleRate | int | Samples per second | 24000 or 48000 |
| Channels | int | Audio channels | 1 (mono) or 2 (stereo) |
| BitsPerSample | int | Bit depth | 16 (PCM S16LE) |
| DurationMs | int | Audio duration in milliseconds | > 0 |
| Timestamp | DateTimeOffset | When frame was captured/generated | Required, UTC timestamp |

**Validation Rules**:
- Data.Length must equal: (SampleRate * Channels * (BitsPerSample/8) * DurationMs) / 1000
- SampleRate must be 24000 (OpenAI) or 48000 (Discord)
- Channels must be 1 (OpenAI) or 2 (Discord)
- BitsPerSample must be 16 (both)

**Format Examples**:
```
Discord Format:  48000 Hz, 2 channels, 16-bit → 192,000 bytes/second
OpenAI Format:   24000 Hz, 1 channel, 16-bit  → 48,000 bytes/second
20ms frame (Discord): 3,840 bytes
20ms frame (OpenAI):  960 bytes
```

---

### 5. OpenAIRealtimeMessage

**Purpose**: WebSocket message schema for OpenAI Realtime API

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| Type | string | Event type (e.g., "session.update") | Required, see OpenAI event types |
| EventId | string? | Unique event identifier | Optional, server-generated |
| Payload | JsonElement | Event-specific data | Required, varies by Type |
| SessionId | string? | OpenAI session identifier | Required after session.created |

**Common Event Types**:
- **Client → Server**: `session.update`, `input_audio_buffer.append`, `input_audio_buffer.commit`, `response.create`, `conversation.item.create`
- **Server → Client**: `session.created`, `session.updated`, `input_audio_buffer.speech_started`, `input_audio_buffer.speech_stopped`, `response.audio.delta`, `response.audio.done`, `response.function_call_arguments.done`

**Validation Rules**:
- Type must be a recognized OpenAI Realtime API event type
- Payload structure must match event type schema (validated against OpenAI spec)
- SessionId required after initial `session.created` event

---

### 6. FunctionCallData

**Purpose**: Details of an AI function call request

**Attributes**:
| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| CallId | string | Unique call identifier | Required, format: call_<id> |
| Name | string | Function name | Required, must match registered plugin |
| Arguments | Dictionary<string, object> | Function parameters | Required, may be empty |
| Status | FunctionCallStatus (enum) | Execution status | Required, see FunctionCallStatus enum |
| Error | string? | Error message if failed | Optional, set when Status=Failed |

**Validation Rules**:
- Name must match a registered Semantic Kernel function
- Arguments must match function parameter schema
- Error only allowed when Status is Failed
- CallId must be unique within a session

---

## Enumerations

### SessionState

```csharp
public enum SessionState
{
    Initializing,   // Creating Discord + OpenAI connections
    Connected,      // WebSockets connected, waiting for audio
    Active,         // Actively processing audio/conversation
    Disconnecting,  // Cleanup in progress
    Completed,      // Successfully closed
    Error           // Error state, cleanup required
}
```

### MessageRole

```csharp
public enum MessageRole
{
    System,     // System instructions/configuration
    User,       // User speech input
    Assistant,  // AI voice output
    Function    // Function call result
}
```

### FunctionCallStatus

```csharp
public enum FunctionCallStatus
{
    Pending,     // Waiting to execute
    Executing,   // Currently running
    Completed,   // Successfully executed
    Failed       // Execution error
}
```

---

## Aggregate Relationships

### Voice Session Hierarchy

```
VoiceSessionState (1)
  ├── VoiceSessionConfig (1)
  └── ConversationContext (0..50)
       └── ConversationMessage (1)
            ├── AudioData? (0..1)
            └── FunctionCall? (0..1)
                 └── FunctionCallData (1)
```

### Audio Processing Flow

```
Discord VoiceNextConnection
  → AudioFrame (48kHz, stereo)
    → AudioConverter.Resample()
      → AudioFrame (24kHz, mono)
        → OpenAIRealtimeClient.SendAudio()
          → OpenAIRealtimeMessage

OpenAIRealtimeMessage (audio.delta)
  → AudioFrame (24kHz, mono)
    → AudioConverter.Resample()
      → AudioFrame (48kHz, stereo)
        → Discord VoiceTransmitSink
```

---

## Data Flow Diagrams

### Session Lifecycle

```
[User: /voice-join]
  → VoiceCommandHandler.JoinAsync()
    → VoiceSessionService.CreateSessionAsync()
      → new VoiceSessionState
        → State: Initializing
      → DiscordClient.ConnectToVoiceAsync()
      → OpenAIRealtimeClient.ConnectAsync()
        → State: Connected
      → WaitForAudio()
        → State: Active
```

### Conversation Flow

```
[User speaks in Discord]
  → DSharpPlus VoiceReceived event
    → AudioFrame (Discord format)
      → AudioConverter.ToOpenAIFormat()
        → AudioFrame (OpenAI format)
          → OpenAIRealtimeClient.SendAudioAsync()
            → OpenAIRealtimeMessage (input_audio_buffer.append)

[OpenAI processes speech]
  → OpenAIRealtimeMessage (response.audio.delta)
    → AudioFrame (OpenAI format)
      → ConversationMessage (role: Assistant)
      → AudioConverter.ToDiscordFormat()
        → AudioFrame (Discord format)
          → VoiceTransmitSink.WriteAsync()
            → [User hears AI response]
```

### Function Calling Flow

```
[AI decides to call function]
  → OpenAIRealtimeMessage (response.function_call_arguments.done)
    → FunctionCallData (name: "get_weather", arguments: {"city": "Seattle"})
      → Status: Pending
      → VoiceSessionService.ExecuteFunctionAsync()
        → Status: Executing
        → Semantic Kernel plugin invocation
          → WeatherService.GetWeatherAsync("Seattle")
        → Status: Completed
      → OpenAIRealtimeMessage (conversation.item.create with function result)
        → ConversationMessage (role: Function, result: "Sunny, 72°F")
```

---

## Persistence Strategy

### Phase 1 (MVP): In-Memory Only

All entities stored in memory within VoiceSessionService:
```csharp
private readonly ConcurrentDictionary<Guid, VoiceSessionState> _activeSessions;
```

**Limitations**:
- Sessions lost on bot restart
- No cross-instance support (single pod deployment)
- No historical analytics

**Advantages**:
- Simple implementation
- Low latency
- No external dependencies

### Phase 2 (Optional): Database Persistence

**Entity Storage**:
| Entity | Storage | Rationale |
|--------|---------|-----------|
| VoiceSessionState | PostgreSQL | Session metadata, queryable history |
| ConversationMessage | PostgreSQL | Conversation history, analytics |
| AudioFrame | Not persisted | Too large, privacy concerns |
| VoiceSessionConfig | PostgreSQL | Reuse config across sessions |

**Schema** (PostgreSQL):
```sql
CREATE TABLE voice_sessions (
    session_id UUID PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ,
    state VARCHAR(20) NOT NULL,
    participant_count INT,
    total_cost_cents INT,
    INDEX idx_guild_channel (guild_id, channel_id),
    INDEX idx_start_time (start_time DESC)
);

CREATE TABLE conversation_messages (
    message_id VARCHAR(50) PRIMARY KEY,
    session_id UUID REFERENCES voice_sessions(session_id),
    role VARCHAR(20) NOT NULL,
    content TEXT,
    function_call JSONB,
    timestamp TIMESTAMPTZ NOT NULL,
    token_count INT,
    INDEX idx_session_timestamp (session_id, timestamp)
);

CREATE TABLE voice_session_configs (
    config_id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    max_session_duration_minutes INT,
    voice_model VARCHAR(20),
    enable_function_calling BOOLEAN,
    created_at TIMESTAMPTZ NOT NULL,
    UNIQUE (guild_id)
);
```

---

## Cost Tracking Model

### Session Cost Calculation

```csharp
public class SessionCostTracker
{
    // OpenAI Realtime API pricing (per 1M tokens)
    private const decimal TextInputCostPer1M = 5.00m;
    private const decimal TextOutputCostPer1M = 20.00m;
    private const decimal AudioInputCostPer1M = 100.00m;
    private const decimal AudioOutputCostPer1M = 200.00m;

    public decimal CalculateSessionCost(VoiceSessionState session)
    {
        decimal totalCost = 0;

        foreach (var message in session.ConversationContext)
        {
            if (message.Role == MessageRole.User && message.AudioData != null)
            {
                // Audio input cost
                var tokens = EstimateTokensFromAudio(message.AudioData.Length);
                totalCost += (tokens / 1_000_000m) * AudioInputCostPer1M;
            }
            else if (message.Role == MessageRole.Assistant)
            {
                if (message.AudioData != null)
                {
                    // Audio output cost
                    var tokens = EstimateTokensFromAudio(message.AudioData.Length);
                    totalCost += (tokens / 1_000_000m) * AudioOutputCostPer1M;
                }
                if (message.Content != null)
                {
                    // Text output cost (transcription)
                    var tokens = message.TokenCount;
                    totalCost += (tokens / 1_000_000m) * TextOutputCostPer1M;
                }
            }
        }

        return totalCost;
    }

    private int EstimateTokensFromAudio(int audioByteLength)
    {
        // Rough estimate: 1 token ≈ 0.5 seconds of audio
        // Audio: 24kHz mono 16-bit = 48,000 bytes/second
        var seconds = audioByteLength / 48000.0;
        return (int)(seconds * 2); // 2 tokens per second
    }
}
```

---

## Validation Summary

All entities include validation attributes in their C# implementations:

```csharp
public class VoiceSessionState
{
    [Required]
    public Guid SessionId { get; init; }

    [Required]
    [Range(1, ulong.MaxValue)]
    public ulong GuildId { get; init; }

    [Required]
    [Range(1, ulong.MaxValue)]
    public ulong ChannelId { get; init; }

    [Required]
    public DateTimeOffset StartTime { get; init; }

    [Required]
    public DateTimeOffset LastActivityTime { get; set; }

    [Required]
    [EnumDataType(typeof(SessionState))]
    public SessionState State { get; set; }

    [Range(0, int.MaxValue)]
    public int ParticipantCount { get; set; }

    [Required]
    [MaxLength(50)]
    public List<ConversationMessage> ConversationContext { get; init; } = new();

    [Required]
    public VoiceSessionConfig Configuration { get; init; }
}
```

---

**Data Model Status**: Complete ✓
**Next Step**: Generate API contracts for voice commands and WebSocket events
