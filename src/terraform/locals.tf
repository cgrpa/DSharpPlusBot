locals {
  common_tags = {
    Environment = var.environment
    Application = var.application
    Location    = var.location
  }
  name_prefix         = "${var.environment}-${var.location_short}-${var.application}"
  name_prefix_no_dash = "${var.environment}${var.location_short}${var.application}"

  required_secret_alias_map = {
    DiscordToken     = "discord-token"
    GeminiKey        = "gemini-key"
    GrokKey          = "grok-key"
    OpenRouterApiKey = "openrouter-api-key"
    TavilyApiKey     = "tavily-api-key"
  }

  required_secret_keys = toset(var.required_secret_names)
  alias_map_keys       = toset(keys(local.required_secret_alias_map))

  required_secret_aliases = {
    for key in var.required_secret_names : key => local.required_secret_alias_map[key]
    if contains(keys(local.required_secret_alias_map), key)
  }

  existing_key_vault_secrets_by_name = {
    for secret in data.azurerm_key_vault_secrets.required.secrets : secret.name => secret
  }

  missing_required_secret_names = [
    for secret_name in var.required_secret_names : secret_name
    if !contains(keys(local.existing_key_vault_secrets_by_name), secret_name)
  ]

  disabled_required_secret_names = [
    for secret_name in var.required_secret_names : secret_name
    if contains(keys(local.existing_key_vault_secrets_by_name), secret_name) && !local.existing_key_vault_secrets_by_name[secret_name].enabled
  ]

  container_app_secret_aliases = var.enforce_required_secret_presence ? local.required_secret_aliases : {
    for secret_name, secret_alias in local.required_secret_aliases : secret_name => secret_alias
    if contains(keys(local.existing_key_vault_secrets_by_name), secret_name) && local.existing_key_vault_secrets_by_name[secret_name].enabled
  }
}
