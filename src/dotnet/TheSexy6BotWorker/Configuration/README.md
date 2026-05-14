# Bot Configuration System

## Architecture Overview

The bot system uses a **Registry + Strategy Pattern** to support multiple AI models with different configurations. Each bot is defined by implementing `IBotConfiguration`, which specifies:

- Command prefix (`gemini`, `grok`, etc.)
- Semantic Kernel service ID
- System prompt/personality
- Execution settings (mutable for runtime changes)
- Capabilities (reply chains, function calling, images)

## Semantic Kernel Package Baseline

- Core Semantic Kernel packages should track the current stable family.
- `Microsoft.SemanticKernel.Connectors.Google` is an intentional alpha exception because a stable Google connector channel is not currently available.

## Adding a New Bot

### 1. Create Bot Configuration

Create a new file in `Configuration/` (e.g., `ClaudeBotConfiguration.cs`):

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Configuration;

public class ClaudeBotConfiguration : IBotConfiguration
{
    public string Prefix => "claude";
    
    public string ServiceId => "claude";
    
    public string SystemMessage => "You are Claude, a helpful AI assistant.";
    
    public PromptExecutionSettings Settings { get; set; } = new OpenAIPromptExecutionSettings
    {
        MaxTokens = 8192,
        Temperature = 0.7
    };
    
    public bool SupportsReplyChains => true;
    
    public bool SupportsFunctionCalling => true;
    
    public bool SupportsImages => true;
}
```

### 2. Register in Semantic Kernel

In `DiscordWorker.cs`, add the chat completion service to the Kernel builder:

```csharp
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "claude-3-5-sonnet-20241022",
    apiKey: _configuration["ClaudeKey"],
    endpoint: new Uri("https://api.anthropic.com/v1/"),
    serviceId: "claude");
```

### 3. Register in BotRegistry

In `DiscordWorker.cs`, add to the bot registry:

```csharp
services.AddSingleton(sp =>
{
    var registry = new BotRegistry(messagePrefix);
    registry.Register(new GeminiBotConfiguration());
    registry.Register(new GrokBotConfiguration());
    registry.Register(new ClaudeBotConfiguration()); // ← Add here
    return registry;
});
```

### 4. Add User Secret

```bash
dotnet user-secrets set "ClaudeKey" "your-api-key-here"
```

That's it! The new bot will automatically:
- Respond to `claude <message>` in normal mode
- Respond to `test-claude <message>` in Development mode
- Use the specified settings and capabilities
- Support all features enabled in the configuration

## Modifying Bot Settings at Runtime

Settings are mutable via the `Settings` property:

```csharp
var bot = _botRegistry.GetBot("grok");
if (bot?.Settings is OpenAIPromptExecutionSettings openAISettings)
{
    openAISettings.Temperature = 0.9;
    openAISettings.MaxTokens = 2048;
}
```

This allows future tool calls to dynamically adjust bot behavior (temperature, max tokens, function calling behavior, etc.).

## Current Bots

### Gemini (`gemini`)
- **Model**: gemini-2.5-flash-lite
- **Service**: Google AI
- **Features**: Fast responses, basic chat
- **Function Calling**: No
- **Reply Chains**: No
- **Images**: No

### Grok (`grok`)
- **Model**: grok-4-fast-non-reasoning
- **Service**: X.AI
- **Features**: Uncensored, witty, function calling
- **Function Calling**: Yes (auto)
- **Reply Chains**: Yes (10 messages deep)
- **Images**: Yes

## Benefits of This Architecture

✅ **SOLID Principles**
- Single Responsibility: Each config handles one bot
- Open/Closed: Add bots without modifying handler
- Dependency Inversion: Handler depends on abstraction

✅ **Easy Testing**
- Mock `IBotConfiguration` for unit tests
- Test registry independently
- Test handler with fake bots

✅ **Runtime Flexibility**
- Mutable settings for dynamic behavior
- Easy to add admin commands to change bot configs
- Could load from JSON in the future

✅ **Clean Code**
- Handler reduced from ~300 to ~150 lines
- No duplicated processing logic
- Bot-specific logic isolated

## Tavily API Configuration

Tavily tools are wired directly as Semantic Kernel plugin functions through `TavilyApiService` (`tavily_search`, `tavily_extract`, `tavily_crawl`, `tavily_map`).

Runtime configuration is under `TavilyApi`:

```json
{
  "TavilyApi": {
    "Endpoint": "https://api.tavily.com",
    "TimeoutSeconds": 30,
    "MaxRetries": 2,
    "BaseDelayMilliseconds": 250,
    "MaxDelayMilliseconds": 4000
  }
}
```

Contracts:

- Authentication uses project API key `TavilyApiKey` from user secrets or environment variables.
- Success responses return raw Tavily JSON.
- Failures return structured JSON payloads from the tool (no thrown exception path to the model).
- Retry policy is bounded exponential backoff + jitter for transient failures (HTTP `429`, `5xx`, and transport/network failures).
- Non-retryable `4xx` responses return structured failure immediately.
- `tavily_research` is intentionally excluded from this v1 integration.
