# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TheSexy6BotWorker is a Discord bot built as a .NET 9.0 Worker Service that integrates multiple AI models (Google Gemini and X.AI Grok-4) with Microsoft Semantic Kernel for function calling capabilities. The bot responds to Discord messages with AI-powered chat completions, provides weather information and web search capabilities through Semantic Kernel plugins, and supports multimodal image inputs for Grok-4. The bot features dynamic status updates that reflect recent conversations and real-time voice conversations using OpenAI's Realtime API (gpt-realtime-mini).

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

**MessageCreatedHandler** (`Handlers/MessageCreatedHandler.cs:20`)
- Handles all Discord message events
- Routes messages to appropriate AI model based on prefix (`gemini` or `grok`)
- Manages conversation history for threaded replies (up to 10 messages deep via `GetReplyChainAsync`)
- Chunks long responses into multiple messages (max 1980 chars per message)
- Grok has a custom system prompt for uncensored, witty responses
- **Multimodal Support**: Detects image attachments and embeds, sending them as separate chat messages with direct URL links for Grok-4's vision capabilities (`AddImageMessagesAsync:223-300`)
- Formats messages with attachment and embed information using `FormatMessageWithAttachments`

### Semantic Kernel Integration

The bot uses Microsoft Semantic Kernel v1.65.0 for AI orchestration with function calling:

**Kernel Configuration** (`DiscordWorker.cs:69-91`)
- Two chat completion services:
  - Gemini 2.5 Flash Lite (free tier: 1500 requests/day)
  - Grok-4-Fast-Non-Reasoning (multimodal with vision support)
- Plugins registered as kernel functions for automatic function calling
- Services injected as singletons for the bot's lifetime

**Available Plugins**:
1. **PerplexitySearchService** - Web search via Perplexity API
2. **WeatherService** - Weather data from Open-Meteo API (geocoding + forecast)

**DynamicStatusService** (`Services/DynamicStatusService.cs`)
- Automatically updates Discord bot status based on conversation activity
- Uses Gemini to generate witty status messages reflecting recent conversations
- Switches to idle status after 15 minutes of inactivity
- Timer checks status every 5 minutes

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
- `OpenAIApiKey` - OpenAI API key for Realtime voice API (gpt-4o-realtime-preview)
- `LOCAL_DEV` - Optional, adds "test-" prefix to command triggers

**User Secrets**: Project uses .NET User Secrets (ID: `dotnet-TheSexy6BotWorker-d23e68fa-7622-4b43-ac67-735c9cf191f4`)

### Command System

The bot supports two types of commands:

1. **DSharpPlus Text Commands** - Prefix-based (`/command_name`)
   - Configured with `TextCommandProcessor` in `DiscordWorker.cs:110-119`
   - `/ping` - Simple ping/pong test (`Commands/PingCommand.cs`)
   - `/voice_join` - Joins the voice channel you're currently in (`Commands/VoiceCommand.cs`)
   - `/voice_leave` - Leaves the current voice channel (`Commands/VoiceCommand.cs`)

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
Only Grok is configured with `FunctionChoiceBehavior.Auto()` for automatic function calling (`MessageCreatedHandler.cs:28-32`). Gemini uses standard prompt execution without function calling.

### Multimodal Image Support
Grok-4-Fast-Non-Reasoning supports vision capabilities. The bot:
1. Includes attachment URLs and embed information in the text message context
2. Sends separate chat messages with image data using `ImageContent` with direct Discord CDN URLs
3. Supports .jpg, .jpeg, .png, .gif, .webp, .bmp formats
4. Falls back to base64 encoding if direct URLs don't work
5. Handles both message attachments and embed images/thumbnails

### Dynamic Status Updates
The bot's Discord status reflects its current activity:
- **Active**: After each Grok interaction, Gemini generates a witty status message (max 128 chars) based on the conversation topic
- **Idle**: After 15 minutes of inactivity, displays random idle messages like "waiting on my humans to need me again"
- Status update timer runs every 5 minutes
- Uses Gemini for cost efficiency (free tier: 1500 requests/day)

### Voice Support

The bot supports real-time voice conversations using OpenAI's Realtime API with the `gpt-4o-realtime-preview-2024-12-17` model. Users can summon the bot into voice channels for natural voice interactions with function calling support.

**Architecture**:
1. **VoiceService** (`Services/VoiceService.cs`) - Orchestrates Discord VoiceNext and Realtime API
2. **RealtimeService** (`Services/RealtimeService.cs`) - Manages OpenAI WebSocket connections
3. **AudioConverter** (`Services/AudioConverter.cs`) - Converts audio between Discord (48kHz stereo) and OpenAI (24kHz mono) formats
4. **RealtimeToolAdapter** (`Services/RealtimeToolAdapter.cs`) - Adapts Semantic Kernel plugins to Realtime API tools

**Audio Flow**:
```
Discord User (48kHz stereo PCM)
  → VoiceReceived event
  → AudioConverter.ConvertDiscordToOpenAI (downsample to 24kHz mono)
  → RealtimeService WebSocket → OpenAI
  → AI Response (24kHz mono)
  → AudioConverter.ConvertOpenAIToDiscord (upsample to 48kHz stereo)
  → Discord Voice Transmit Stream
```

**Voice Commands**:
- `/voice_join` - Bot joins your current voice channel and starts a Realtime API session
- `/voice_leave` - Bot leaves the voice channel and ends the session

**Features**:
- Server-side Voice Activity Detection (VAD) for natural turn-taking
- Automatic disconnection after 30 seconds of inactivity
- Function calling support (weather, web search) during voice conversations
- Real-time audio streaming with low latency

**Configuration**:
- VoiceNext enabled with `EnableIncoming = true` in `DiscordWorker.cs:105-108`
- Realtime API uses Alloy voice, PCM16 audio format, and Whisper-1 transcription
- Session instructions configured for Discord voice chat context

**Dependencies**:
- DSharpPlus.VoiceNext (5.0.0-nightly-02551)
- Azure.AI.OpenAI (2.3.0-beta.2)
- NAudio (2.2.1)
- **FFmpeg** - Required in application directory for audio encoding/decoding

**Notes**:
- Only one active voice session per Discord guild
- Audio conversion uses NAudio's MediaFoundationResampler
- Inactivity timer checks every 10 seconds
- Function calls are handled asynchronously during conversations

## CI/CD

GitHub Actions workflow (`.github/workflows/docker-ci.yml`) builds and pushes Docker images to Azure Container Registry on every push. Images are tagged with both commit SHA and `latest`.