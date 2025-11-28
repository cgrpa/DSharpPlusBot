# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TheSexy6BotWorker is a Discord bot built as a .NET 9.0 Worker Service that integrates multiple AI models (Google Gemini and X.AI Grok) with Microsoft Semantic Kernel for function calling capabilities. The bot responds to Discord messages with AI-powered chat completions and provides weather information and web search capabilities through Semantic Kernel plugins.

**NEW (In Development)**: Voice Integration - Real-time voice conversations in Discord using DSharpPlus VoiceNext and OpenAI Realtime API. See [Voice Integration](#voice-integration-in-development) section below.

## Common Commands

### Build & Run
```bash
# Build the solution
dotnet build

# Run the main worker service
dotnet run --project TheSexy6BotWorker/TheSexy6BotWorker.csproj

# Run with local development settings (adds "test-" prefix to bot commands)
LOCAL_DEV=true dotnet run --project TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run integration tests only (requires API access)
dotnet test --filter "Category=Integration"

# Run a specific test file
dotnet test --filter "FullyQualifiedName~WeatherKernelIntegrationTest"
```

### Docker
```bash
# Build Docker image
docker build -t thesexy6bot .

# Run in container (requires environment variables)
docker run -e DiscordToken=<token> -e GeminiKey=<key> -e GrokKey=<key> thesexy6bot
```

## Architecture

### Core Components

**DiscordWorker** (`DiscordWorker.cs:20`)
- Implements `BackgroundService` as the main worker service
- Configures DSharpPlus Discord client with command and event handling
- Sets up dependency injection for services and Semantic Kernel
- Registers two AI chat completion services (Gemini and Grok) with different service IDs
- Registers Semantic Kernel plugins for weather and web search functionality

**MessageCreatedHandler** (`Handlers/MessageCreatedHandler.cs:18`)
- Handles all Discord message events
- Routes messages to appropriate AI model based on prefix (`gemini` or `grok`)
- Manages conversation history for threaded replies (up to 10 messages deep via `GetReplyChainAsync`)
- Chunks long responses into multiple messages (max 1980 chars per message)
- Grok has a custom system prompt for uncensored, witty responses

### Semantic Kernel Integration

The bot uses Microsoft Semantic Kernel v1.65.0 for AI orchestration with function calling:

**Kernel Configuration** (`DiscordWorker.cs:67-88`)
- Two chat completion services: Gemini (flash-lite) and Grok (mini)
- Plugins registered as kernel functions for automatic function calling
- Services injected as singletons for the bot's lifetime

**Available Plugins**:
1. **PerplexitySearchService** - Web search via Perplexity API
2. **WeatherService** - Weather data from Open-Meteo API (geocoding + forecast)

### Service Architecture

**PerplexitySearchService** (`Services/PerplexitySearchService.cs`)
- Exposes `[KernelFunction("perplexity_search")]` for AI to call
- Requires `PerplexityApiKey` configuration
- Returns structured search results via DTOs

**WeatherService** (`Services/WeatherService.cs`)
- Exposes `[KernelFunction("get_weather")]` with city and units parameters
- Uses two HttpClients: one for geocoding, one for weather data
- Geocodes city name to coordinates, then fetches weather
- Returns formatted weather report with emojis

### Configuration

**Required Environment Variables/User Secrets**:
- `DiscordToken` - Discord bot token (validated with GuardClauses)
- `GeminiKey` - Google AI Gemini API key
- `GrokKey` - X.AI Grok API key
- `PerplexityApiKey` - Perplexity API key
- `LOCAL_DEV` - Optional, adds "test-" prefix to command triggers
- **Voice Integration (NEW)**:
  - `OpenAI:RealtimeApiKey` - OpenAI Realtime API key for voice integration
  - `OpenAI:RealtimeModel` - Model name (default: "gpt-4o-realtime-preview-2024-10-01")
  - `VoiceIntegration:DefaultBudgetPerServerUSD` - Per-server cost budget (default: 100)
  - `VoiceIntegration:MaxSessionDurationMinutes` - Max session duration (default: 10)
  - `VoiceIntegration:EnableCostTracking` - Enable cost tracking (default: true)

**User Secrets**: Project uses .NET User Secrets (ID: `dotnet-TheSexy6BotWorker-d23e68fa-7622-4b43-ac67-735c9cf191f4`)

**Configure Voice Integration Secrets**:
```bash
dotnet user-secrets set "OpenAI:RealtimeApiKey" "sk-proj-..."
dotnet user-secrets set "OpenAI:RealtimeModel" "gpt-4o-realtime-preview-2024-10-01"
dotnet user-secrets set "VoiceIntegration:DefaultBudgetPerServerUSD" "100"
dotnet user-secrets set "VoiceIntegration:MaxSessionDurationMinutes" "10"
dotnet user-secrets set "VoiceIntegration:EnableCostTracking" "true"
```

### Command System

The bot supports two types of commands:

1. **DSharpPlus Text Commands** - Prefix-based (`/ping`)
   - Configured with `TextCommandProcessor` in `DiscordWorker.cs:98-101`
   - Example: `PingCommand` in `Commands/PingCommand.cs`

2. **Message Content Triggers** - Handled in `MessageCreatedHandler`
   - `gemini` or `test-gemini` - Routes to Gemini AI
   - `grok` or `test-grok` - Routes to Grok AI with reply chain support
   - `ping` - Simple pong response

### Project Structure

```
TheSexy6BotWorker/          # Main worker service project
├── Commands/               # DSharpPlus command definitions
├── Handlers/               # Discord event handlers
├── Services/               # Semantic Kernel plugin services
├── DTOs/                   # Data transfer objects for API responses
├── Program.cs             # Entry point, DI setup
└── DiscordWorker.cs       # Main background service

TheSexy6BotWorker.Tests/    # xUnit test project
└── Services/               # Integration tests for services
```

## Development Notes

### Local Development Mode
When running with `LOCAL_DEV=true` environment variable, all command prefixes are changed from production to test mode (e.g., `gemini` becomes `test-gemini`). This is configured in `MessageCreatedHandler.cs:20`.

### Message Reply Chains
The Grok integration supports threaded conversations by walking up the reply chain to build conversation history. Maximum depth is 10 messages to prevent excessive token usage (`MessageCreatedHandler.cs:144-166`).

### HttpClient Configuration
Services use named HttpClients configured in `DiscordWorker.cs`:
- `PerplexitySearchService` - Single client with auth header
- `WeatherService` - Two clients (WeatherClient, GeocodingClient) injected via factory pattern

### Function Calling Behavior
Only Grok is configured with `FunctionChoiceBehavior.Auto()` for automatic function calling (`MessageCreatedHandler.cs:26-30`). Gemini uses standard prompt execution without function calling.

## CI/CD

GitHub Actions workflow (`.github/workflows/docker-ci.yml`) builds and pushes Docker images to Azure Container Registry on every push. Images are tagged with both commit SHA and `latest`.

---

## Voice Integration (In Development)

**Feature Branch**: `001-voice-integration`
**Status**: Planning Phase Complete, Ready for Implementation
**Documentation**: `/specs/001-voice-integration/`

### Overview

Voice integration enables real-time AI conversations in Discord voice channels using:
- **DSharpPlus.VoiceNext 4.5.1**: Discord voice channel connection and audio streaming
- **OpenAI Realtime API**: Low-latency speech-to-speech AI processing (WebSocket-based)
- **NAudio**: Audio format conversion (Discord 48kHz stereo ↔ OpenAI 24kHz mono)

### Key Commands (Planned)

| Command | Description | Prefix (Dev/Prod) |
|---------|-------------|-------------------|
| `voice-join` | Summon bot to voice channel | test-voice-join / voice-join |
| `voice-leave` | Disconnect from voice channel | test-voice-leave / voice-leave |
| `voice-status` | Show active session info | test-voice-status / voice-status |
| `voice-config` | Configure session settings (Admin) | test-voice-config / voice-config |
| `voice-stats` | View usage statistics (Admin) | test-voice-stats / voice-stats |

### Architecture (Planned)

```
TheSexy6BotWorker/
├── Commands/
│   └── VoiceCommands.cs           # Voice control commands (NEW)
├── Handlers/
│   └── VoiceEventHandler.cs       # Voice session events (NEW)
├── Services/
│   └── Voice/                     # Voice integration services (NEW)
│       ├── VoiceSessionService.cs # Session management (singleton)
│       ├── OpenAIRealtimeClient.cs # WebSocket client for OpenAI
│       ├── AudioConverter.cs      # Format conversion (48kHz ↔ 24kHz)
│       └── ConversationContextManager.cs # Context tracking
└── DTOs/
    └── Voice/                     # Voice-related DTOs (NEW)
        ├── VoiceSessionState.cs   # Session metadata
        ├── OpenAIRealtimeMessage.cs # WebSocket message schemas
        ├── AudioFrame.cs          # Audio data container
        └── VoiceSessionConfig.cs  # Session configuration
```

### Native Dependencies (NEW)

**Required for voice integration**:
- **libopus**: Opus audio codec (Discord voice encoding)
- **libsodium**: Voice encryption for Discord
- **ffmpeg**: Audio format conversion

**Installation**:
```bash
# macOS (Development)
brew install opus libsodium ffmpeg

# Linux/Docker (Production)
apt-get install libopus0 libsodium23 ffmpeg
```

**Windows**: Automatically included via NuGet packages

### Configuration (NEW User Secrets)

```bash
# OpenAI Realtime API
dotnet user-secrets set "OpenAI:RealtimeApiKey" "sk-proj-..."
dotnet user-secrets set "OpenAI:RealtimeModel" "gpt-4o-realtime-preview-2024-10-01"

# Voice Integration Settings
dotnet user-secrets set "VoiceIntegration:DefaultBudgetPerServerUSD" "100"
dotnet user-secrets set "VoiceIntegration:MaxSessionDurationMinutes" "10"
dotnet user-secrets set "VoiceIntegration:EnableCostTracking" "true"
```

### Audio Processing Flow (Planned)

```
[User speaks in Discord voice channel]
  → DSharpPlus VoiceReceived event (48kHz stereo PCM)
    → AudioConverter.ToOpenAIFormat() (→ 24kHz mono PCM)
      → OpenAIRealtimeClient.SendAudioAsync() (WebSocket)
        → OpenAI processes speech + generates response
      → OpenAIRealtimeClient receives audio deltas (24kHz mono PCM)
    → AudioConverter.ToDiscordFormat() (→ 48kHz stereo PCM)
  → Discord VoiceTransmitSink.WriteAsync()
[User hears AI response]
```

### Function Calling Integration (Planned)

Existing Semantic Kernel plugins (WeatherService, PerplexitySearchService) will be mapped to OpenAI Realtime API tool definitions, enabling voice-triggered function calls:

```
User: "What's the weather in Seattle?" (spoken)
  → OpenAI Realtime API decides to call get_weather function
  → VoiceSessionService executes WeatherService.GetWeatherAsync("Seattle")
  → Result returned to OpenAI
  → AI responds: "It's sunny and 72 degrees in Seattle" (spoken)
```

### Cost Management (Planned)

- **OpenAI Realtime API Pricing**: ~$0.35-0.40 per conversation minute
- **Per-server budget limits**: Configurable via `voice-config`
- **Session time limits**: Default 10 minutes, configurable
- **Cost tracking**: Real-time cost estimation, accessible via `voice-stats`

### Testing (Planned)

**Unit Tests**:
- Audio format conversion logic
- Session state management
- Conversation context tracking

**Integration Tests** (Category=Integration):
- DSharpPlus voice connection lifecycle
- OpenAI Realtime WebSocket communication
- Function calling in voice context
- Error scenarios (disconnection, API failures)

**Manual Testing** (Required):
- Real Discord voice channel interactions
- Multi-user voice scenarios
- Network instability resilience

### Reference Materials

- **Specification**: `/specs/001-voice-integration/spec.md`
- **Implementation Plan**: `/specs/001-voice-integration/plan.md`
- **Research Findings**: `/specs/001-voice-integration/research.md`
- **Data Model**: `/specs/001-voice-integration/data-model.md`
- **Command Contracts**: `/specs/001-voice-integration/contracts/voice-commands.md`
- **OpenAI API Contract**: `/specs/001-voice-integration/contracts/openai-realtime-api.md`
- **Quick Start Guide**: `/specs/001-voice-integration/quickstart.md`
- **Reference Demo**: `/OpenAI-CSharpRealtimeWPFDemo/` (local project)

### Implementation Status

- ✅ Phase 0: Research (DSharpPlus VoiceNext, OpenAI Realtime API)
- ✅ Phase 1: Design (Data model, contracts, quickstart)
- ✅ Phase 2: Setup & Foundational (NuGet packages, DTOs, AudioConverter)
- ✅ Phase 3: User Story 1 - MVP (Voice join/leave commands) **CURRENT**
- ⏳ Phase 4: User Story 2 - Voice conversations
- ⏳ Phase 5-9: Additional features (flow, cleanup, functions, cost, polish)

### Voice Command Usage (MVP - Phase 3)

**Available Commands**:
```bash
/voice-join          # Summon bot to your current voice channel
/test-voice-join     # Development mode alias (LOCAL_DEV=true)

/voice-leave         # Disconnect bot from voice channel
/test-voice-leave    # Development mode alias (LOCAL_DEV=true)
```

**Example Workflow**:
```
1. User joins a Discord voice channel
2. User types: /voice-join
3. Bot responds: "✅ Joined General Voice! Speak naturally and I'll respond.
                  Duration limit: 10 minutes"
4. Bot connects to voice channel (MVP: connection only, voice conversation in Phase 4)
5. User types: /voice-leave
6. Bot responds: "👋 Left General Voice.
                  Session duration: 0h 2m 15s
                  Messages exchanged: 0"
7. Bot disconnects from voice channel
```

**Service Architecture**:
- **VoiceSessionService** (Singleton): Manages all active voice sessions
  - Tracks sessions per guild (one bot instance per server)
  - Handles Discord VoiceNext connections
  - Enforces session limits (duration, cost, timeouts)
- **AudioConverter** (Transient): Converts between Discord (48kHz stereo) and OpenAI (24kHz mono)
- **Session State**: Initializing → Connected → Active → Disconnecting → Completed

**Configuration** (appsettings.json or user secrets):
```json
{
  "VoiceIntegration": {
    "MaxSessionDurationMinutes": 10,
    "AutoDisconnectOnSilenceSeconds": 300,
    "EnableFunctionCalling": true,
    "VoiceModel": "alloy",
    "Temperature": 0.8,
    "MaxContextMessages": 20,
    "CostLimitCents": null
  }
}
```

**Error Handling**:
- User not in voice channel → "❌ You must be in a voice channel to use this command!"
- Bot already connected → "❌ I'm already in a voice channel. Use `/voice-leave` first."
- Missing permissions → "❌ I don't have permission to join that channel!"
- Command timeout (30s) → "❌ Command timed out. Please try again."

**Logging**:
- Structured logging with session IDs, guild IDs, channel IDs
- Logs session lifecycle events (create, connect, disconnect, complete)
- Logs participant count and session configuration

**Next Step**: Implement Phase 4 (User Story 2) to add actual voice conversation capability