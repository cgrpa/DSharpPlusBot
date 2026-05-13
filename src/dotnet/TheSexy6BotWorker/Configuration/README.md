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

## MCP Configuration (Disabled by Default)

MCP rollout is controlled under the `Mcp` section. The default contract is intentionally non-breaking:

- `Mcp:Enabled` defaults to `false`
- `Mcp:StrictStartup` defaults to `false`

`Mcp:Servers` is a named map of server configs. Each server supports endpoint, headers, tool allowlist, and startup timeout controls:

```json
{
  "Mcp": {
    "Enabled": false,
    "StrictStartup": false,
    "Servers": {
      "Tavily": {
        "Endpoint": "https://mcp.tavily.com/mcp",
        "Headers": {
          "Authorization": "Bearer ${TavilyApiKey}"
        },
        "AllowedTools": [
          "search"
        ],
        "Startup": {
          "ConnectTimeoutSeconds": null,
          "InitializeTimeoutSeconds": null,
          "ReadyTimeoutSeconds": null
        }
      }
    }
  }
}
```

The `${TavilyApiKey}` placeholder is interpolation syntax. Define `TavilyApiKey` in user-secrets or environment variables and keep `Mcp:Enabled=false` until rollout is ready.

Interpolation and validation contract:

- Placeholder resolution order is configuration first, then OS environment variable fallback.
- If interpolation cannot resolve one or more placeholders, only that MCP server is skipped (degraded startup contract).
- Tavily `DEFAULT_PARAMETERS` is supported via headers and must be valid JSON; malformed JSON marks only that server as skipped.

Registration and discovery contract:

- Server bootstrap runs in parallel across configured MCP servers.
- Per-server startup timeout defaults to `10` seconds.
- Per-server timeout overrides can be set via `Startup.ConnectTimeoutSeconds`, `Startup.InitializeTimeoutSeconds`, and `Startup.ReadyTimeoutSeconds`; the most restrictive non-null value is used as the startup timeout budget.
- Transport auto-detection order is `StreamableHttp` first, then `ServerSentEvents` fallback.
- Only `AllowedTools` are registered into the kernel plugin.
- If any configured allowed tool is missing from discovery, that entire server is skipped (no partial registration).
- Tavily plugin alias is fixed as `TavilyRemoteMcp`; non-Tavily aliases are deterministic (`<ServerName>RemoteMcp`).

Runtime invocation contract and prompt guidance:

- If an MCP tool is unavailable at runtime (server disconnected, connect failure, or tool outside the fixed allowlist), invocation returns an explicit failure message.
- Failure text must be treated as authoritative: `This call failed and no non-MCP fallback was executed.`
- Prompt/tool behavior should not silently substitute a different path when MCP is unavailable. The assistant should communicate the failure clearly and ask the user whether to retry or proceed without MCP-backed data.
