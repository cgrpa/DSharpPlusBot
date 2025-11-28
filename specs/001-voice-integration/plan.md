# Implementation Plan: Discord Voice Integration

**Branch**: `001-voice-integration` | **Date**: 2025-11-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-voice-integration/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enable real-time voice conversations between Discord users and AI through the bot. Users can summon the bot to a voice channel using commands, speak naturally into their microphones, and receive AI-generated voice responses. The implementation combines DSharpPlus VoiceNext (Discord voice handling) with OpenAI Realtime API (low-latency AI voice processing) with automatic audio format conversion.

**Key Technical Approach**:
- DSharpPlus VoiceNext for Discord voice channel connection and audio streaming
- OpenAI Realtime API (WebSocket) for speech-to-speech AI conversations
- NAudio + FFmpeg for audio format conversion (48kHz Discord ↔ 24kHz OpenAI)
- Voice Session Service (singleton) to manage concurrent voice channel connections
- Integration with existing Semantic Kernel plugins for function calling in voice conversations

## Technical Context

**Language/Version**: C# 12 / .NET 9.0 (existing project framework)
**Primary Dependencies**:
- DSharpPlus 5.0.0-nightly-02551 (Discord client, existing)
- DSharpPlus.VoiceNext 4.5.1 (Discord voice integration, new)
- Microsoft.SemanticKernel 1.65.0 (AI orchestration, existing)
- NAudio 2.2.1 (audio processing, new)
- System.Net.WebSockets (OpenAI Realtime API, existing in .NET)
- Native: libopus, libsodium, ffmpeg (audio codecs, new)

**Storage**: In-memory conversation context (Phase 1), optional PostgreSQL/Redis persistence (Phase 2)

**Testing**: xUnit with integration test categories (existing pattern)
- Unit tests for audio format conversion, session management
- Integration tests for Discord voice connection, OpenAI WebSocket
- Manual testing in Discord voice channels (required by constitution)

**Target Platform**: Linux Docker containers (existing deployment), macOS/Windows for development

**Project Type**: Single project - .NET Worker Service with DI (aligns with existing architecture)

**Performance Goals**:
- < 500ms end-to-end latency (Discord → OpenAI → Discord)
- < 2s voice command execution (summon/dismiss)
- 3+ concurrent voice sessions without performance degradation
- 95%+ voice command success rate

**Constraints**:
- Audio format mismatch: Discord (48kHz stereo) vs OpenAI (24kHz mono) requires resampling
- OpenAI Realtime API cost: ~$0.35-0.40 per conversation minute
- WebSocket reliability: dual connections (Discord + OpenAI) require robust reconnection logic
- Multi-user voice channels: sequential processing due to OpenAI 1:1 conversation design
- Native dependencies: libopus, libsodium must be available in deployment environment

**Scale/Scope**:
- Target: 10-50 active Discord servers
- Concurrent voice sessions: 3-10 simultaneously
- Session duration: 1-10 minutes per conversation
- Monthly budget: $100-500 per server (configurable cost limits)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ PASSING GATES

**I. Service-Oriented Architecture**:
- ✅ VoiceSessionService implemented as injectable singleton
- ✅ OpenAIRealtimeClient registered in DI container
- ✅ Separate handler class for voice commands (VoiceCommandHandler)
- ✅ Integration tests planned for voice session lifecycle

**II. AI Model Flexibility**:
- ✅ OpenAI Realtime API aligns with existing multi-model architecture
- ✅ Could add Azure OpenAI Realtime in future (same API contract)
- ✅ Maintains conversation history handling pattern
- ✅ Function calling preserved through tool definitions

**III. Function Calling via Semantic Kernel Plugins**:
- ✅ Existing plugins (WeatherService, PerplexitySearchService) mapped to OpenAI tool definitions
- ✅ [KernelFunction] attributes translated to OpenAI function schemas
- ✅ Integration tests required for voice-triggered function calls
- ✅ Structured DTOs maintained (no raw string responses)

**IV. Configuration Security & Environment Awareness**:
- ✅ OpenAI Realtime API key stored in User Secrets (dev) / Environment Variables (prod)
- ✅ GuardClauses validation at VoiceSessionService startup
- ✅ LOCAL_DEV mode support for test voice commands (e.g., `test-voice-join`)
- ✅ No secrets committed to source control

**V. Observability & Conversation Context**:
- ✅ Structured logging for WebSocket events, audio processing, function calls
- ✅ Conversation context maintained across exchanges (in-memory)
- ✅ Error handling with user-friendly Discord messages
- ✅ Audio session events logged for debugging

**VI. Testing Philosophy (NON-NEGOTIABLE)**:
- ✅ Unit tests planned for:
  - Audio format conversion logic
  - Session state management
  - Conversation context tracking
- ✅ Integration tests planned for:
  - DSharpPlus voice connection lifecycle
  - OpenAI Realtime WebSocket communication
  - Function calling in voice context
  - Error scenarios (disconnection, API failures)
- ✅ Manual testing required for Discord voice flows (per constitution)

**VII. Clean Architecture (Pragmatic Application)**:
- ✅ Layer separation:
  - Handlers: VoiceCommandHandler (Discord command processing)
  - Services: VoiceSessionService, OpenAIRealtimeClient, AudioConverter
  - DTOs: VoiceSessionState, OpenAIRealtimeMessage, AudioFrame
  - Infrastructure: WebSocket client, NAudio audio processors
- ✅ Handlers delegate to services (no business logic in handlers)
- ✅ No circular dependencies

**VIII. Dependency Injection (MANDATORY)**:
- ✅ All services registered in Program.cs
- ✅ VoiceSessionService: Singleton (stateful WebSocket management)
- ✅ AudioConverter: Transient (stateless format conversion)
- ✅ Named HttpClients not applicable (using WebSockets)
- ✅ GuardClauses validation for all injected dependencies

**IX. Creative Problem-Solving & Innovation**:
- ✅ Innovative use of dual WebSocket connections (Discord + OpenAI)
- ✅ Creative audio format bridging (48kHz ↔ 24kHz resampling)
- ✅ Pragmatic multi-user handling (sequential processing vs complex mixing)
- ✅ Cost-aware design (per-server budgets, session limits)

### ⚠️ COMPLEXITY JUSTIFICATIONS

None required - all design decisions align with constitution principles.

## Project Structure

### Documentation (this feature)

```text
specs/001-voice-integration/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (COMPLETE)
├── data-model.md        # Phase 1 output (next)
├── quickstart.md        # Phase 1 output (next)
├── contracts/           # Phase 1 output (next)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

**Structure Decision**: Single project structure (Option 1) - aligns with existing TheSexy6BotWorker architecture. Voice integration is an extension of the existing Worker Service, not a separate application.

```text
TheSexy6BotWorker/                     # Main worker service project (existing)
├── Commands/                          # Discord text commands (existing)
│   ├── PingCommand.cs
│   └── VoiceCommands.cs              # NEW: Voice control commands (join/leave)
├── Handlers/                          # Discord event handlers (existing)
│   ├── MessageCreatedHandler.cs
│   └── VoiceEventHandler.cs          # NEW: Voice session event handling
├── Services/                          # Application services (existing)
│   ├── PerplexitySearchService.cs
│   ├── WeatherService.cs
│   ├── Voice/                         # NEW: Voice integration services
│   │   ├── VoiceSessionService.cs    # Manages voice sessions per channel
│   │   ├── OpenAIRealtimeClient.cs   # WebSocket client for OpenAI Realtime API
│   │   ├── AudioConverter.cs         # Format conversion (48kHz ↔ 24kHz)
│   │   └── ConversationContextManager.cs # Context tracking for voice sessions
├── DTOs/                              # Data transfer objects (existing)
│   ├── PerplexitySearchResponse.cs
│   ├── WeatherData.cs
│   └── Voice/                         # NEW: Voice-related DTOs
│       ├── VoiceSessionState.cs      # Session metadata and state
│       ├── OpenAIRealtimeMessage.cs  # WebSocket message schemas
│       ├── AudioFrame.cs             # Audio data container
│       └── VoiceSessionConfig.cs     # Session configuration
├── Program.cs                         # DI registration (existing, will be updated)
└── DiscordWorker.cs                   # Main worker (existing, will be updated)

TheSexy6BotWorker.Tests/               # Test project (existing)
├── Services/                          # Integration tests (existing)
│   ├── WeatherServiceTests.cs
│   ├── Voice/                         # NEW: Voice service tests
│   │   ├── VoiceSessionServiceTests.cs
│   │   ├── OpenAIRealtimeClientTests.cs
│   │   ├── AudioConverterTests.cs
│   │   └── Integration/
│   │       ├── VoiceConnectionTests.cs
│   │       └── VoiceFunctionCallingTests.cs
```

### Native Dependencies (Docker/Linux)

```dockerfile
# Dockerfile (existing, will be updated)
FROM mcr.microsoft.com/dotnet/aspnet:9.0

RUN apt-get update && apt-get install -y \
    libopus0 \                        # NEW: Opus audio codec
    libsodium23 \                     # NEW: Discord voice encryption
    ffmpeg \                          # NEW: Audio format conversion
    && rm -rf /var/lib/apt/lists/*

COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "TheSexy6BotWorker.dll"]
```

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**Status**: No violations identified. All design decisions comply with constitution principles:
- Service-Oriented Architecture: Voice services follow existing pattern
- DI: All services registered with appropriate lifetimes
- Clean Architecture: Handlers → Services → DTOs separation maintained
- Testing: Unit + integration tests planned
- Observability: Structured logging throughout

N/A - No complexity justifications required.
