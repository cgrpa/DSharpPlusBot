# DSharpPlusBot

A Discord bot built as a .NET 9.0 Worker Service that integrates multiple AI models (Google Gemini and X.AI Grok) with Microsoft Semantic Kernel for function calling capabilities. Features a modular bot architecture with engagement mode for autonomous conversation participation.

## Features

- **Multi-Bot Architecture**: Pluggable bot configurations via `IBotConfiguration` interface
- **Dual AI Integration**: 
  - Google Gemini (flash-lite) - Basic chat completion
  - X.AI Grok (fast-non-reasoning) - Full featured with function calling, images, engagement mode
- **Engagement Mode**: Bots can participate in ongoing channel conversations without explicit invocation
  - Sliding context window with 3-minute session timeout
  - Bot autonomously decides whether to respond to non-prefixed messages
  - Rate limiting during high activity (5+ messages in 15 seconds)
- **Semantic Kernel Plugins**: 
  - Weather data via Open-Meteo API (no API key required)
  - Web search via Perplexity API
- **Threaded Conversations**: Reply chain context (up to 10 messages deep)
- **Dynamic Status**: Bot updates Discord status with witty AI-generated messages (batched, rate-limited)
- **Local Development Mode**: Run with test command prefixes for safe testing

## Architecture

### Bot Abstraction Layer

The bot system is built around `IBotConfiguration`, enabling different AI personalities and capabilities:

```csharp
public interface IBotConfiguration
{
    string Prefix { get; }                    // Command trigger (e.g., "grok", "gemini")
    string ServiceId { get; }                 // Semantic Kernel service ID
    string SystemMessage { get; }             // Bot personality prompt
    PromptExecutionSettings Settings { get; set; }
    
    // Capabilities
    bool SupportsReplyChains { get; }
    bool SupportsFunctionCalling { get; }
    bool SupportsImages { get; }
    
    // Engagement Mode
    bool SupportsEngagementMode { get; }
    string? EngagementModeInstructions { get; }
    TimeSpan SessionTimeout { get; }
    // ... rate limiting settings
}
```

### Engagement Mode Flow

Engagement mode uses a **two-phase approach** with structured output:

```
User says "grok hello"
    │
    └─> Session created for channel
        Bot MUST respond (direct invocation)
        │
        └─> Subsequent messages in channel (without prefix)
            │
            ├─> Phase 1: Tool gathering (search, weather) with Auto function calling
            │   └─> Bot can research before deciding
            │
            └─> Phase 2: Structured decision via ResponseFormat
                │
                ├─> {"shouldRespond": true, "message": "..."} → Send message
                └─> {"shouldRespond": false} → Silence
                    │
                    └─> Session expires after 3 min inactivity
```

The `EngagementDecision` model enforces typed JSON output:
```csharp
public class EngagementDecision
{
    public bool ShouldRespond { get; set; }
    public string? Message { get; set; }
}
```

### Bot Configurations

| Bot | Prefix | Reply Chains | Function Calling | Images | Engagement Mode |
|-----|--------|--------------|------------------|--------|-----------------|
| Gemini | `gemini` | ❌ | ❌ | ❌ | ❌ |
| Grok | `grok` | ✅ | ✅ | ✅ | ✅ |

Grok's engagement mode personality:
> *"You're opinionated and enjoy banter. Jump into conversations that interest you."*

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Discord bot token with Message Content intent enabled
- API keys for:
  - Google AI Gemini
  - X.AI Grok
  - Perplexity
- (Optional) Docker for containerized deployment

## Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd DSharpPlusBot
```

### 2. Configure User Secrets

This project uses .NET User Secrets to store sensitive configuration. Set up your secrets with:

```bash
# Set Discord bot token
dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj set "DiscordToken" "your-discord-bot-token"

# Set Google AI Gemini API key
dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj set "GeminiKey" "your-gemini-api-key"

# Set X.AI Grok API key
dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj set "GrokKey" "your-grok-api-key"

# Set Tavily API key
dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj set "TavilyApiKey" "your-tavily-api-key"
```

**User Secrets ID**: `dotnet-TheSexy6BotWorker-d23e68fa-7622-4b43-ac67-735c9cf191f4`

> **Note**: User secrets are stored locally and are not checked into source control. For production deployments, use environment variables or Azure Key Vault.

### 3. Verify Configuration

List your configured secrets:

```bash
dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj list
```

### 4. Restore Dependencies

```bash
dotnet restore TheSexy6BotWorker.slnx
```

## Running Locally

For remote staging secret wiring and runbook steps, see `src/terraform/README.md`.

### Standard Mode (Production Commands)

```bash
dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

In Discord:
- `gemini <message>` - Chat with Gemini AI
- `grok <message>` - Chat with Grok AI (starts engagement session)
- `ping` - Test bot responsiveness
- `/ping` - DSharpPlus slash command

### Engagement Mode Usage

1. Say `grok hello` to start a session
2. Continue chatting naturally - Grok sees all messages
3. Grok decides when to jump in or stay quiet
4. Session auto-expires after 3 minutes of inactivity

### Local Development Mode (Test Commands)

Set `DOTNET_ENVIRONMENT=Development` to add the `test-` command prefix:

```bash
# PowerShell
$env:DOTNET_ENVIRONMENT="Development"; dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj

# Bash/Linux
DOTNET_ENVIRONMENT=Development dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

Commands become: `test-gemini`, `test-grok`, `test-ping`

## Testing

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test categories
dotnet test --filter "FullyQualifiedName~BotConfiguration"
dotnet test --filter "FullyQualifiedName~ConversationSession"
dotnet test --filter "FullyQualifiedName~Markdown"
```

## Docker Build and Deployment

### Local Docker Build

```bash
docker build -t thesexy6bot:latest .
```

### Run Docker Container

```bash
docker run -e DiscordToken="your-token" \
           -e GeminiKey="your-key" \
           -e GrokKey="your-key" \
           -e TavilyApiKey="your-key" \
           thesexy6bot:latest
```

### Build and Push to Azure Container Registry

```bash
# Login to Azure Container Registry
az acr login --name thesexy6botregistry

# Build and push
docker buildx build --platform linux/amd64 \
  -t thesexy6botregistry.azurecr.io/thesexy6bot:v1.0.0 \
  -t thesexy6botregistry.azurecr.io/thesexy6bot:latest \
  --push .
```

## Project Structure

```
DSharpPlusBot/
├── src/
│   ├── dotnet/
│   │   ├── TheSexy6BotWorker/       # Main worker project
│   │   └── TheSexy6BotWorker.Tests/ # xUnit test project
│   └── terraform/                   # Azure infra + secret contract
├── Dockerfile
├── TheSexy6BotWorker.slnx
└── README.md
```

## Core Components

### BotRegistry
Routes messages to appropriate bot based on prefix:
```csharp
if (_botRegistry.TryGetBot(message, out var bot, out var strippedMessage))
{
    // Process with bot
}
```

### ConversationSessionManager
Thread-safe session management for engagement mode:
- Tracks active sessions per channel
- Handles session expiry (sliding window)
- Rate limiting during high activity

### DynamicStatusService
AI-generated Discord status updates:
- Batches last 5 interactions for context
- Minimum 2-minute interval between updates
- Uses Gemini for status generation

### Markdown Library
Fluent builder for generating structured markdown:
```csharp
var md = new ObjectMarkdownBuilder<Config>(config)
    .Section("Settings", s => s
        .Property(c => c.Name, icon: "🔧")
        .Property(c => c.Value))
    .Build();
```

## Configuration Reference

| Key | Description | Required |
|-----|-------------|----------|
| `DiscordToken` | Discord bot token | Yes |
| `GeminiKey` | Google AI Gemini API key | Yes |
| `GrokKey` | X.AI Grok API key | Yes |
| `TavilyApiKey` | Tavily API key | Yes |
| `DOTNET_ENVIRONMENT` | Set to `Development` to enable test command prefixes | No |

## Key Dependencies

- **DSharpPlus 5.0.0-nightly-02551**: Discord API wrapper
- **Microsoft.SemanticKernel 1.65.0**: AI orchestration framework
- **Microsoft.SemanticKernel.Connectors.Google 1.65.0-alpha**: Gemini integration
- **Ardalis.GuardClauses 5.0.0**: Input validation

## Troubleshooting

### Bot Not Responding
- Verify all user secrets are set: `dotnet user-secrets --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj list`
- Check Discord bot has Message Content intent enabled
- Ensure bot has appropriate permissions in your Discord server

### Engagement Mode Issues
- Session expires after 3 minutes - say the bot prefix again to restart
- Bot uses structured output to decide - check `EngagementDecision` in logs
- Two-phase approach: Phase 1 (tools) then Phase 2 (decision)
- Check logs for session start/end events

### API Rate Limits (429 Errors)
- DynamicStatusService now batches requests (5 messages, 2 min minimum interval)
- Check Gemini/Grok API quotas

### Docker Build Issues
- Ensure you're using .NET 9.0 SDK
- For ACR push: `az acr login --name thesexy6botregistry`

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
