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
  - Tavily direct API tools (`tavily_search`, `tavily_extract`, `tavily_crawl`, `tavily_map`)
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
- [Terraform CLI](https://developer.hashicorp.com/terraform/install) (1.9+ recommended)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Discord bot token with Message Content intent enabled
- API keys for:
  - Google AI Gemini
  - X.AI Grok
  - Tavily
- (Optional) Docker with BuildKit/Buildx for containerized deployment

## Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd TheSexy6BotWorker
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

## Terraform Setup (Azure Infrastructure)

Terraform lives in `src/terraform` and provisions the Azure Resource Group, Storage Account, Container Registry, Key Vault, Log Analytics Workspace, and Container App.

### CI/CD Bootstrap Flow (Recommended)

This is the expected deployment sequence for workload identity + GitHub Actions:

1. Bootstrap the required Azure resource group scope(s) used by your pipeline identity.
2. Grant the pipeline service principal both `Contributor` and `User Access Administrator` on those resource group scope(s) so Terraform can create infra and RBAC assignments.
3. Run the pipeline once with `enforce_required_secret_presence = false` to create infrastructure before Key Vault secrets exist.
4. Populate required Key Vault secrets.
5. Re-run the pipeline with strict mode enabled (`enforce_required_secret_presence = true`) so deploy succeeds only when required secrets are present/enabled.

`User Access Administrator` is required because this Terraform module creates `azurerm_role_assignment` resources.

Example role grants for a pipeline service principal:

```bash
SCOPE="/subscriptions/<subscription-id>/resourceGroups/<bootstrap-or-target-rg>"
SP_OBJECT_ID="<service-principal-object-id>"

az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "Contributor" \
  --scope "$SCOPE"

az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "User Access Administrator" \
  --scope "$SCOPE"
```

### 1. Authenticate to Azure (Local)

```bash
az login
az account set --subscription "<your-subscription-id>"
export ARM_SUBSCRIPTION_ID="<your-subscription-id>"
```

### 2. Initialize Terraform (Local Backend)

```bash
terraform -chdir=src/terraform init -backend-config=local.tfbackend
```

### 3. First Pipeline Deploy Bootstrap (Strict Secret Checks Off)

The module enforces required Key Vault secrets by default. If this is a brand-new environment, temporarily enable bootstrap mode in `src/terraform/terraform.tfvars`:

```hcl
enforce_required_secret_presence = false
```

Then run the deployment pipeline once to create infrastructure:

- `ci-cd.yml` on `main`, or
- `workflow_dispatch` with the Terraform reusable workflow

```bash
# local equivalent (if you need to reproduce outside CI):
terraform -chdir=src/terraform apply
```

### 4. Seed Required Secrets in Key Vault

The required secret keys are: `DiscordToken`, `GeminiKey`, `GrokKey`, `TavilyApiKey`.

```bash
KEY_VAULT_NAME="$(terraform -chdir=src/terraform output -raw key_vault_name)"

KEY_VAULT_NAME="$KEY_VAULT_NAME" \
DISCORD_TOKEN="your-discord-token" \
GEMINI_KEY="your-gemini-key" \
GROK_KEY="your-grok-key" \
TAVILY_API_KEY="your-tavily-api-key" \
./src/terraform/scripts/upsert-required-secrets.sh --non-interactive
```

### 5. Re-enable Strict Secret Checks and Re-deploy

Set `enforce_required_secret_presence = true` (or remove the override), then re-run the deployment pipeline. Local equivalent:

```bash
terraform -chdir=src/terraform plan
terraform -chdir=src/terraform apply
terraform -chdir=src/terraform output
```

For additional secret-rotation runbook details, see `src/terraform/README.md`.

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
- `/tools` or `/plugins` - List callable Kernel tools

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

### OpenTelemetry + Aspire Dashboard (Standalone)

The worker exports logs, traces, and metrics using OTLP (`AddOtlpExporter`).

Start the Aspire Dashboard locally:

```bash
docker run --rm -it -d \
  -p 18888:18888 \
  -p 4317:18889 \
  -p 4318:18890 \
  --name aspire-dashboard \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

Run the bot (it will export to OTLP defaults, including `http://localhost:4317` for gRPC):

```bash
dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

Open the dashboard UI at `http://localhost:18888`.

Optional explicit exporter settings:

```bash
# Bash/Linux
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj

# PowerShell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL="grpc"
dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

Disable OTEL temporarily:

```bash
OTEL_SDK_DISABLED=true dotnet run --project src/dotnet/TheSexy6BotWorker/TheSexy6BotWorker.csproj
```

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

Tavily integration tests are live-network and opt-in:

```bash
# Enable live Tavily API tests + provide key
RUN_TAVILY_LIVE_TESTS=true \
TAVILY_API_KEY=tvly-your-key \
dotnet test --filter "FullyQualifiedName~TavilyApiIntegrationTests"
```

`TavilyApiIntegrationTests` also accepts `TavilyApiKey` from user secrets and `TAVILY_API_ENDPOINT` to override the default endpoint (`https://api.tavily.com`).

## Docker Setup and Deployment

### 1. Build the Image

```bash
docker build \
  --build-arg GIT_SHA="$(git rev-parse --short HEAD)" \
  --build-arg GIT_COMMIT_MSG="local" \
  -t thesexy6bot:latest .
```

### 2. Run Container Smoke Test

```bash
docker run --rm thesexy6bot:latest --smoke-test
```

### 3. Run the Bot Container Locally

```bash
docker run --rm \
  -e DiscordToken="your-discord-token" \
  -e GeminiKey="your-gemini-key" \
  -e GrokKey="your-grok-key" \
  -e TavilyApiKey="your-tavily-api-key" \
  thesexy6bot:latest
```

### 4. Push to Azure Container Registry

Get registry values from Terraform outputs:

```bash
ACR_LOGIN_SERVER="$(terraform -chdir=src/terraform output -raw container_registry_login_server)"
ACR_NAME="${ACR_LOGIN_SERVER%%.azurecr.io}"
IMAGE_TAG="$(git rev-parse --short HEAD)"

az acr login --name "$ACR_NAME"

docker buildx build --platform linux/amd64 \
  -t "${ACR_LOGIN_SERVER}/discordbot:${IMAGE_TAG}" \
  -t "${ACR_LOGIN_SERVER}/discordbot:latest" \
  --push .
```

## Project Structure

```
TheSexy6BotWorker/
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
- For ACR push, derive the registry name from Terraform output: `ACR_LOGIN_SERVER="$(terraform -chdir=src/terraform output -raw container_registry_login_server)"; az acr login --name "${ACR_LOGIN_SERVER%%.azurecr.io}"`

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, validation, and pull request guidelines.
