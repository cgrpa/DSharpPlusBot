# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build TheSexy6BotWorker.sln

# Run all non-integration tests
dotnet test --filter "Category!=Integration"

# Run a single test class
dotnet test --filter "FullyQualifiedName~BotRegistryTests"

# Run with integration tests (requires valid API keys in user secrets)
dotnet test

# Run the bot locally (requires user secrets configured)
dotnet run --project TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

User secrets ID: `dotnet-TheSexy6BotWorker-d23e68fa-7622-4b43-ac67-735c9cf191f4`

In `Development` environment, command prefixes get `test-` prepended (e.g. `test-grok`, `test-gemini`). This is controlled in `GrokBotConfiguration.cs` and `GeminiBotConfiguration.cs` by reading `ASPNETCORE_ENVIRONMENT`.

## Architecture

This is a .NET 9 Worker Service Discord bot that routes messages to multiple AI backends via Microsoft Semantic Kernel. The key abstraction is `IBotConfiguration` — each implementation represents a distinct bot personality with its own command prefix, AI model, system prompt, and capability flags.

### Message routing flow

1. `MessageCreatedHandler` receives all Discord messages
2. `BotRegistry` matches the message prefix (case-insensitive) to a registered `IBotConfiguration`
3. **Direct invocation** (prefix matched): the matched bot must respond; Semantic Kernel runs with Auto function calling
4. **Engagement mode** (no prefix, active session exists): two-phase LLM call:
   - Phase 1: gather context via plugins (search, weather)
   - Phase 2: return structured `EngagementDecision` JSON (`shouldRespond: bool, message?: string`)
5. `ConversationSession` tracks per-channel chat history, rate limiting, and token estimation

### Current bots

- **Gemini** (`GeminiBotConfiguration`): Google gemini-2.5-flash-lite, basic chat, no function calling, no engagement mode
- **Grok** (`GrokBotConfiguration`): X.AI grok-4-fast-non-reasoning via OpenAI-compatible endpoint, full function calling, engagement mode enabled

### Semantic Kernel plugins

- `PerplexitySearchService`: web search (requires `PERPLEXITY_API_KEY`)
- `WeatherService`: weather via Open-Meteo (no key required) + geocoding

### Adding a new bot

Implement `IBotConfiguration`, register it in `DiscordWorker.cs` where the other bots are registered via the Semantic Kernel builder and `BotRegistry`. Use `BotConfigurationExtensions` for reflection-based helpers.

### Key services

- `BotRegistry`: thread-safe prefix→bot lookup
- `ConversationSessionManager`: session lifecycle with sliding expiry and rate-limit detection (5+ messages in 15 s → 2–4 s delay)
- `DynamicStatusService`: batches 5 interactions, then calls Gemini to generate a witty bot status; minimum 2-minute interval between updates

### Markdown builder

`TheSexy6BotWorker/Markdown/` contains a custom fluent builder used to render bot configurations as Discord messages. `ObjectMarkdownBuilder<T>` uses compiled lambda expressions with `[MarkdownProperty]`, `[MarkdownIgnore]`, and `[MarkdownSection]` attributes.

## Deployment

CI/CD via GitHub Actions:
- **`docker-ci.yml`**: on push to `main`, builds Docker image and pushes to Azure Container Registry (tagged with git SHA and `latest`)
- **`pr-validation.yml`**: on PRs, runs `dotnet build` + `dotnet test --filter "Category!=Integration"`

The Dockerfile is a standard multi-stage build (SDK → publish → mcr.microsoft.com/dotnet/runtime).
