# TheSexy6BotWorker

A Discord bot built as a .NET 9.0 Worker Service that integrates multiple AI models (Google Gemini and X.AI Grok) with Microsoft Semantic Kernel for function calling capabilities.

## CI/CD Pipeline

The project uses GitHub Actions for continuous integration and deployment:

### Workflow Overview

1. **Test Job** - Runs on every push
   - Checks out code
   - Sets up .NET 9.0
   - Restores dependencies
   - Builds the solution
   - Runs all integration tests (Weather API and Perplexity API)

2. **Build and Deploy Job** - Only runs if tests pass
   - Builds Docker images (tagged with commit SHA and `latest`)
   - Pushes images to Azure Container Registry (ACR)
   - Deploys to Azure Container Apps with a new revision

### Required GitHub Secrets

Configure the following secrets in your repository settings:

- `AZURE_CREDENTIALS` - Azure service principal credentials for authentication
- `REGISTRY_LOGIN_SERVER` - ACR login server URL
- `REGISTRY_USERNAME` - ACR username
- `REGISTRY_PASSWORD` - ACR password
- `AZURE_CONTAINER_APP_NAME` - Name of the Azure Container App
- `AZURE_RESOURCE_GROUP` - Azure resource group name
- `PERPLEXITY_API_KEY` - API key for Perplexity search integration tests

### Quality Gate

The pipeline includes an automated quality gate:
- Docker images are only built and pushed if all tests pass
- Deployment only occurs after successful push to ACR
- This ensures that only verified code is deployed to production

## Development

See [CLAUDE.md](CLAUDE.md) for detailed development documentation.