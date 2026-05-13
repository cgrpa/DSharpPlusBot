# Terraform: Azure Key Vault Remote Secret Contract

## Local Terraform Plan

1. Set subscription context:
`export ARM_SUBSCRIPTION_ID="<your-subscription-id>"`
2. Initialize:
`terraform init -backend-config=local.tfbackend`
3. Plan:
`terraform plan`

## Secret Contract

- Required runtime keys are:
  - `DiscordToken`
  - `GeminiKey`
  - `GrokKey`
  - `PerplexityApiKey`
- Terraform maps those keys to Container App secret aliases:
  - `DiscordToken -> discord-token`
  - `GeminiKey -> gemini-key`
  - `GrokKey -> grok-key`
  - `PerplexityApiKey -> perplexity-api-key`
- Remote runtime uses Key Vault references only (no secret values in Terraform config/state).

## Enforcement Controls

- `required_secret_names`:
  - Defaults to the four runtime keys above.
  - Must exactly match the alias-map keys (parity check enforced by precondition).
- `enforce_required_secret_presence`:
  - Defaults to `true`.
  - When `true`, Terraform fails fast if required secrets are missing/disabled in Key Vault.
  - When `false`, Terraform allows bootstrap mode and only wires currently present+enabled secrets.

## Bootstrap / Rotation Script

Use `scripts/upsert-required-secrets.sh` to set all required secrets in Key Vault.

Get the target vault name from Terraform output:

```bash
terraform output -raw key_vault_name
```

Example:

```bash
KEY_VAULT_NAME="stg-uks-discordbot-kv" \
DISCORD_TOKEN="..." \
GEMINI_KEY="..." \
GROK_KEY="..." \
PERPLEXITY_API_KEY="..." \
./scripts/upsert-required-secrets.sh
```

Notes:

- The script defaults to all-or-nothing updates.
- Missing values are prompted securely when interactive.
- Use `--non-interactive` in CI/automation.
- Use `--allow-partial` only for explicit recovery workflows.

## Post-Rotation Refresh (Deterministic)

Container Apps can take time to pick up rotated values automatically. For deterministic cutover, restart active revisions:

```bash
APP_NAME="<container-app-name>"
RESOURCE_GROUP="<resource-group-name>"

for REVISION in $(az containerapp revision list \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?properties.active].name" \
  --output tsv); do
  az containerapp revision restart \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --revision "$REVISION"
done
```

