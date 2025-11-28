# Research: Discord Voice Integration

**Feature**: 001-voice-integration
**Date**: 2025-11-25
**Status**: Complete

## Overview

This document consolidates research findings for implementing real-time voice conversations between Discord users and AI through the TheSexy6BotWorker bot. The integration combines DSharpPlus VoiceNext (Discord voice handling) with OpenAI Realtime API (AI voice processing).

---

## Research Areas

### 1. DSharpPlus VoiceNext (Discord Voice Library)

**Decision**: Use DSharpPlus.VoiceNext 4.5.1 (current stable) or 5.0.0-nightly (preview)

**Rationale**:
- Mature, actively maintained Discord library for .NET
- Native voice support through VoiceNext extension
- .NET 9.0 compatible (targets .NET Standard 2.0)
- Extensive documentation and community support
- Aligns with existing DSharpPlus usage in the project

**Alternatives Considered**:
- **Discord.Net**: Alternative Discord library
  - Rejected: Project already uses DSharpPlus; consistency preferred
- **Custom WebSocket + Discord Gateway**: Direct implementation
  - Rejected: Excessive complexity; reinventing the wheel
- **DSharpPlus Voice Wrapper**: Third-party abstraction
  - Rejected: Unnecessary abstraction layer; VoiceNext is sufficient

---

### 2. OpenAI Realtime API (AI Voice Processing)

**Decision**: Use OpenAI Realtime API with gpt-4o-realtime-preview-2024-10-01

**Rationale**:
- Low-latency WebSocket-based voice conversation (300-500ms end-to-end)
- Server-side Voice Activity Detection (VAD) simplifies turn-taking
- Native function calling support (integrates with existing Semantic Kernel plugins)
- Automatic conversation state management (32k token context)
- Reference implementation available locally (`OpenAI-CSharpRealtimeWPFDemo`)

**Alternatives Considered**:
- **Whisper (transcription) + GPT (text) + TTS (synthesis)**: Three-step pipeline
  - Rejected: Higher latency (~2-5 seconds), complex orchestration
- **Azure Speech Services + GPT**: Microsoft ecosystem
  - Rejected: Requires additional services, more complex setup
- **Google Cloud Speech-to-Text + Dialogflow**: Google ecosystem
  - Rejected: Not aligned with existing OpenAI/X.AI model usage
- **Semantic Kernel with Whisper Plugin**: Custom implementation
  - Rejected: No real-time streaming, higher latency

---

### 3. Audio Format Conversion

**Decision**: Use NAudio library with FFmpeg for format conversion

**Rationale**:
- **Format Mismatch Challenge**:
  - Discord Voice: 48kHz, 16-bit stereo PCM (required by Discord)
  - OpenAI Realtime: 24kHz, 16-bit mono PCM (required by OpenAI)
- **NAudio** provides resampling and format conversion in .NET
- **FFmpeg** (already used in demo project) handles complex audio processing
- Both libraries are mature, well-documented, and widely used

**Audio Flow**:
```
Discord (48kHz stereo) → [Resample] → OpenAI (24kHz mono) → [Process] →
OpenAI (24kHz mono) → [Resample] → Discord (48kHz stereo)
```

**Alternatives Considered**:
- **SoX**: Command-line audio processing
  - Rejected: Additional system dependency; NAudio is pure .NET
- **Custom resampling**: Manual implementation
  - Rejected: Complex, error-prone; NAudio is battle-tested
- **Azure Audio Processing**: Cloud-based conversion
  - Rejected: Adds latency and cost

---

### 4. Native Dependencies

**Decision**: Use native Opus and libsodium libraries

**Rationale**:
- **Required by Discord**: Voice encryption (libsodium) and codec (Opus)
- **Automatic on Windows**: NuGet package includes native DLLs
- **Manual installation on Linux/macOS**: Package manager installation required
- **Docker support**: Add to Dockerfile (already using Linux-based images)

**Deployment Strategy**:
- **Windows Development**: Automatic via NuGet
- **macOS Development**: `brew install opus libsodium`
- **Linux/Docker Production**: `apt-get install libopus0 libsodium23 ffmpeg`

**Alternatives Considered**:
- **Pure managed codec**: .NET-only implementation
  - Rejected: Doesn't exist for Opus; performance would be poor
- **Bundled binaries**: Include in repository
  - Rejected: Platform-specific, increases repo size
- **Cloud audio encoding**: Offload to external service
  - Rejected: Adds latency and cost

---

### 5. Voice Session Management

**Decision**: Implement per-channel voice session service with singleton lifetime

**Rationale**:
- Discord allows **one bot instance per voice channel**
- Multiple Discord channels require **concurrent session management**
- **Stateful sessions**: Maintain WebSocket connection, audio buffers, conversation context
- **Singleton service**: Manages all voice sessions globally (aligns with constitution)

**Session Lifecycle**:
1. **Join Command**: Create VoiceSession, connect to Discord + OpenAI
2. **Active State**: Stream audio bidirectionally, maintain context
3. **Leave/Timeout**: Cleanup resources, disconnect both WebSocket connections

**Alternatives Considered**:
- **Scoped service per session**: New instance per channel
  - Rejected: Worker service doesn't support scoped lifetimes well
- **Transient service**: Recreate on each interaction
  - Rejected: Stateful WebSocket connections require persistent instances
- **Static dictionary**: Global state without DI
  - Rejected: Violates constitution (Principle VIII: Dependency Injection)

---

### 6. Conversation Context Persistence

**Decision**: In-memory conversation history with optional database persistence

**Rationale**:
- **OpenAI Limitation**: No automatic session resumption on WebSocket disconnect
- **32k token context**: Conversation history must be restored manually
- **Multi-exchange conversations**: Requires client-side history tracking
- **Cost optimization**: Avoid redundant context transmission

**Implementation Strategy**:
- **Phase 1 (MVP)**: In-memory dictionary (session lifetime only)
- **Phase 2**: Optional Redis/PostgreSQL persistence for cross-session continuity
- **Context Summarization**: Compress history when approaching token limits

**Alternatives Considered**:
- **Pure server-side**: Rely on OpenAI to manage all context
  - Rejected: Doesn't support session resumption
- **Database-only**: Store every message in PostgreSQL
  - Rejected: Over-engineering for MVP; adds latency
- **No persistence**: Lose context on every disconnect
  - Rejected: Poor user experience for multi-turn conversations

---

### 7. Multi-User Voice Channel Handling

**Decision**: Sequential processing with user turn detection

**Rationale**:
- **OpenAI Realtime API Design**: Built for 1:1 conversations
- **Discord Reality**: Voice channels often have multiple simultaneous speakers
- **Server VAD**: Automatically detects when users start/stop speaking
- **Sequential Processing**: Queue user inputs, process one at a time

**Implementation**:
- Bot listens to all users in the voice channel
- Server VAD determines when a user finishes speaking
- Bot responds to one user at a time (natural conversation flow)
- If multiple users speak simultaneously, process in FIFO order

**Alternatives Considered**:
- **Per-user WebSocket**: Separate OpenAI session per Discord user
  - Rejected: Cost prohibitive (~$0.35/min per user)
- **Audio mixing**: Combine all users into single stream
  - Rejected: Loses speaker identification, confuses VAD
- **Permission-based**: Only allow specific users to speak
  - Rejected: Defeats purpose of open voice chat

---

### 8. Function Calling Integration

**Decision**: Map existing Semantic Kernel plugins to OpenAI Realtime API tool definitions

**Rationale**:
- **Existing Infrastructure**: WeatherService, PerplexitySearchService already implemented
- **Function Calling Parity**: OpenAI Realtime supports same function calling as Chat API
- **Unified Experience**: Same tools available in text chat and voice chat
- **Constitution Compliance**: Plugins as first-class citizens (Principle III)

**Mapping Strategy**:
```
Semantic Kernel [KernelFunction]
    → OpenAI Realtime API tool definition
    → Function call event
    → Execute existing plugin
    → Return result to conversation
```

**Alternatives Considered**:
- **Separate voice-only tools**: Different capabilities for voice vs text
  - Rejected: Inconsistent user experience
- **No function calling**: Pure conversation only
  - Rejected: Underutilizes existing bot capabilities
- **Custom function executor**: Bypass Semantic Kernel
  - Rejected: Violates constitution, duplicates code

---

### 9. Error Handling and Resilience

**Decision**: Exponential backoff with graceful degradation

**Rationale**:
- **WebSocket Instability**: Network issues, Discord reconnections, OpenAI API downtime
- **Audio Processing Errors**: Format conversion failures, buffer overflows
- **Graceful Degradation**: Fall back to text-based responses if voice fails
- **User Communication**: Notify users via Discord text messages

**Error Handling Strategy**:
| Error Type | Strategy | Fallback |
|------------|----------|----------|
| WebSocket disconnect | Exponential backoff reconnection (5s, 10s, 30s) | Text message notification |
| Audio format error | Log error, skip frame, continue | Degrade to lower quality |
| OpenAI API error | Retry with backoff, use text completion | Text-based chat response |
| Discord voice disconnect | Cleanup resources, notify user | N/A (expected) |
| Rate limit | Queue requests, throttle | Text message: "Please wait..." |

**Alternatives Considered**:
- **Fail fast**: Disconnect immediately on any error
  - Rejected: Poor user experience; transient errors are common
- **Infinite retry**: Never give up on reconnection
  - Rejected: Resource leak risk, user confusion
- **Silent failures**: Suppress errors, continue silently
  - Rejected: Violates constitution (Principle V: Observability)

---

### 10. Cost Management

**Decision**: Per-server cost tracking with optional limits

**Rationale**:
- **OpenAI Realtime Cost**: ~$0.35-0.40 per minute of conversation
- **Abuse Risk**: Malicious users could rack up costs
- **Discord Server Isolation**: Each server should have separate cost tracking
- **Transparency**: Server owners should know their usage

**Cost Control Measures**:
- **Per-server monthly budgets**: Configurable via bot commands
- **Per-session time limits**: Max 10 minutes per voice session (default)
- **Automatic disconnection**: When budget exhausted, notify and disconnect
- **Usage reporting**: `/voice-stats` command shows current month usage

**Example Cost Scenarios**:
| Scenario | Usage | Monthly Cost |
|----------|-------|--------------|
| Small server (10 users, 1hr/day) | 30 hours | ~$630 |
| Medium server (50 users, 2hr/day) | 100 hours | ~$2,100 |
| Large server (200 users, 3hr/day) | 600 hours | ~$12,600 |

**Alternatives Considered**:
- **No limits**: Unlimited usage
  - Rejected: Financial risk, potential abuse
- **Pay-per-use**: Users pay for their own usage
  - Rejected: Complicates implementation, barrier to adoption
- **Flat rate**: Fixed monthly cost per server
  - Rejected: Not aligned with variable OpenAI pricing

---

## Technical Decisions Summary

| Decision Area | Choice | Rationale |
|---------------|--------|-----------|
| **Discord Library** | DSharpPlus VoiceNext 4.5.1 | Existing integration, .NET 9.0 compatible |
| **AI Provider** | OpenAI Realtime API (gpt-4o) | Low latency, function calling, natural VAD |
| **Audio Processing** | NAudio + FFmpeg | Format conversion, resampling, .NET compatible |
| **Native Dependencies** | Opus, libsodium (via NuGet/package manager) | Required by Discord, automated for Windows |
| **Session Management** | Singleton service with per-channel sessions | Stateful WebSockets, aligns with DI principles |
| **Context Persistence** | In-memory (Phase 1), optional DB (Phase 2) | Simple MVP, scalable architecture |
| **Multi-User Handling** | Sequential processing with Server VAD | Cost-effective, natural conversation flow |
| **Function Calling** | Map existing Semantic Kernel plugins | Reuse infrastructure, consistent UX |
| **Error Handling** | Exponential backoff + graceful degradation | Resilient, user-friendly |
| **Cost Management** | Per-server budgets + session limits | Prevent abuse, financial transparency |

---

## Implementation Dependencies

### NuGet Packages (New):
```xml
<PackageReference Include="DSharpPlus.VoiceNext" Version="4.5.1" />
<PackageReference Include="NAudio" Version="2.2.1" />
```

### System Dependencies (Linux/Docker):
```bash
apt-get install libopus0 libsodium23 ffmpeg
```

### Configuration (New User Secrets):
```json
{
  "OpenAI": {
    "RealtimeApiKey": "[secret]",
    "RealtimeModel": "gpt-4o-realtime-preview-2024-10-01"
  },
  "VoiceIntegration": {
    "DefaultBudgetPerServerUSD": 100,
    "MaxSessionDurationMinutes": 10,
    "EnableCostTracking": true
  }
}
```

---

## Performance Targets

Based on research and demo project analysis:

| Metric | Target | Rationale |
|--------|--------|-----------|
| **End-to-End Latency** | < 500ms | OpenAI: ~300ms, Discord: ~100-200ms |
| **Connection Establishment** | < 2s | WebSocket + Discord connection |
| **Audio Quality (Perceived)** | 90%+ "clear" | Success criterion SC-003 |
| **Concurrent Sessions** | 3+ | Success criterion SC-005 |
| **Session Cleanup Time** | < 5s | Success criterion SC-006 |
| **Voice Command Success** | 95%+ | Success criterion SC-007 |

---

## Security Considerations

1. **API Key Protection**:
   - Store in User Secrets (development) / Environment Variables (production)
   - Never log or transmit in plain text
   - Validate at startup with GuardClauses

2. **Permission Validation**:
   - Verify user is in voice channel before connecting bot
   - Check bot has necessary Discord permissions (Connect, Speak, Use VAD)
   - Validate server owner before enabling cost limits

3. **Audio Privacy**:
   - Audio data transmitted to OpenAI (comply with ToS)
   - No persistent audio recording by default
   - Clear user communication about data handling

4. **Rate Limiting**:
   - Prevent spam of voice commands
   - Implement cooldown between voice sessions
   - Throttle reconnection attempts

---

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| OpenAI API downtime | Medium | High | Fallback to text chat, retry with backoff |
| High costs | Medium | High | Per-server budgets, session limits |
| Poor audio quality | Low | Medium | Format validation, buffer tuning |
| WebSocket instability | Medium | Medium | Reconnection logic, state recovery |
| Multi-user confusion | High | Low | Clear UX, tutorial messages |
| Native dependency issues | Low | Medium | Dockerfile includes all dependencies |

---

## Next Steps

### Phase 1 (Foundation):
1. Implement VoiceSessionService (manage voice connections)
2. Create VoiceCommandHandler (join/leave commands)
3. Integrate DSharpPlus VoiceNext with existing bot

### Phase 2 (AI Integration):
4. Implement OpenAIRealtimeClient (WebSocket management)
5. Add audio format conversion (Discord ↔ OpenAI)
6. Create bidirectional audio streaming pipeline

### Phase 3 (Context & Tools):
7. Implement conversation context management
8. Map Semantic Kernel plugins to OpenAI function definitions
9. Add conversation history persistence

### Phase 4 (Production):
10. Implement cost tracking and limits
11. Add comprehensive error handling
12. Create integration tests for voice session lifecycle

---

## References

### Official Documentation:
- DSharpPlus: https://dsharpplus.github.io/DSharpPlus/
- OpenAI Realtime API: https://platform.openai.com/docs/guides/realtime
- NAudio: https://github.com/naudio/NAudio

### Demo Project:
- Local Path: `/Users/che/Documents/Visual Studio Code/TheSexy6BotWorker/OpenAI-CSharpRealtimeWPFDemo`
- GitHub: https://github.com/fuwei007/OpenAI-CSharpRealtimeWPFDemo

### Discord Resources:
- Discord Voice Gateway: https://discord.com/developers/docs/topics/voice-connections
- DSharpPlus GitHub: https://github.com/DSharpPlus/DSharpPlus

---

**Research Status**: Complete ✓
**Ready for Phase 1 Implementation**: Yes
**Estimated Complexity**: High (realtime audio processing, dual WebSocket management, format conversion)
