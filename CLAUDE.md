# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TheSexy6BotWorker is a Discord bot built as a .NET 9.0 Worker Service that integrates multiple AI models (Google Gemini and X.AI Grok) with Microsoft Semantic Kernel for function calling capabilities. The bot responds to Discord messages with AI-powered chat completions and provides weather information and web search capabilities through Semantic Kernel plugins.

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

**User Secrets**: Project uses .NET User Secrets (ID: `dotnet-TheSexy6BotWorker-d23e68fa-7622-4b43-ac67-735c9cf191f4`)

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

### Workflow Structure

The GitHub Actions workflow (`.github/workflows/docker-ci.yml`) implements a two-stage pipeline:

**Stage 1: Test** (Quality Gate)
- Runs on every push to any branch
- Builds the .NET solution
- Executes Weather API integration tests (requires network access)
- Must pass before Stage 2 can run

**Stage 2: Build and Deploy** (Only if tests pass)
- Builds Docker images tagged with commit SHA and `latest`
- Pushes images to Azure Container Registry (ACR)
- Deploys to Azure Container Apps with a new revision using the commit SHA image

### Test Strategy

The CI/CD pipeline runs Weather API integration tests because:
- They don't require API keys (use public Open-Meteo API)
- They validate real service integration
- Perplexity tests are skipped (require `PerplexityApiKey` secret)

### Required Secrets

Configure these in GitHub repository settings:
- `AZURE_CREDENTIALS` - Azure service principal for authentication
- `REGISTRY_LOGIN_SERVER` - ACR URL
- `REGISTRY_USERNAME` - ACR username
- `REGISTRY_PASSWORD` - ACR password
- `AZURE_CONTAINER_APP_NAME` - Target container app name
- `AZURE_RESOURCE_GROUP` - Azure resource group

### Deployment Flow

```
Push to GitHub → Run Tests → Build Docker Images → Push to ACR → Deploy to Container Apps
                      ↓                                                      ↓
                   If fail: STOP                                    New revision with latest image
```