# Terraform: Azure Key Vault Remote Secret Contract

## CI/CD Bootstrap Sequence

For workload identity pipeline deployments:

1. Bootstrap the Azure resource group scope(s) used for deployment/state.
2. Grant the pipeline service principal both `Contributor` and `User Access Administrator` on those resource group scope(s).
3. Run the pipeline with `enforce_required_secret_presence = false` for the first infrastructure deploy.
4. Populate required Key Vault secrets (`DiscordToken`, `GeminiKey`, `GrokKey`, `TavilyApiKey`).
5. Re-run the pipeline with `enforce_required_secret_presence = true`.

Why `User Access Administrator`: this module creates RBAC role assignments (`azurerm_role_assignment`), which requires permission to assign roles.

Example RBAC grants for a pipeline service principal:

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
  - `TavilyApiKey`
- Terraform maps those keys to Container App secret aliases:
  - `DiscordToken -> discord-token`
  - `GeminiKey -> gemini-key`
  - `GrokKey -> grok-key`
  - `TavilyApiKey -> tavily-api-key`
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
TAVILY_API_KEY="..." \
./scripts/upsert-required-secrets.sh
```

Notes:

- The script defaults to all-or-nothing updates.
- Missing values are prompted securely when interactive.
- Use `--non-interactive` in CI/automation.
- Use `--allow-partial` only for explicit recovery workflows.
- Human operator step: provision/populate `TavilyApiKey` in the target Key Vault before applying Terraform in strict mode.

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
