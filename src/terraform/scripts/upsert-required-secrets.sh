#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  upsert-required-secrets.sh [options]

Options:
  --vault-name <name>         Key Vault name (or set KEY_VAULT_NAME)
  --discord-token <value>     Discord token (or set DISCORD_TOKEN)
  --gemini-key <value>        Gemini API key (or set GEMINI_KEY)
  --grok-key <value>          Grok API key (or set GROK_KEY)
  --perplexity-key <value>    Perplexity API key (or set PERPLEXITY_API_KEY)
  --allow-partial             Allow partial updates (default is all-or-nothing)
  --non-interactive           Fail on missing inputs instead of prompting
  -h, --help                  Show this help
EOF
}

require_option_value() {
  local option_name="$1"
  local option_value="${2:-}"

  if [[ -z "$option_value" || "$option_value" == --* ]]; then
    echo "Missing value for $option_name." >&2
    usage >&2
    exit 1
  fi
}

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command is missing: $command_name" >&2
    exit 1
  fi
}

read_value_if_missing() {
  local label="$1"
  local current_value="$2"

  if [[ -n "$current_value" ]]; then
    printf '%s' "$current_value"
    return
  fi

  if [[ "$NON_INTERACTIVE" == "true" ]]; then
    printf ''
    return
  fi

  local entered_value
  read -r -p "$label: " entered_value
  printf '%s' "$entered_value"
}

read_secret_if_missing() {
  local label="$1"
  local current_value="$2"

  if [[ -n "$current_value" ]]; then
    printf '%s' "$current_value"
    return
  fi

  if [[ "$NON_INTERACTIVE" == "true" ]]; then
    printf ''
    return
  fi

  local entered_value
  read -r -s -p "$label: " entered_value
  echo >&2
  printf '%s' "$entered_value"
}

KEY_VAULT_NAME="${KEY_VAULT_NAME:-}"
DISCORD_TOKEN="${DISCORD_TOKEN:-}"
GEMINI_KEY="${GEMINI_KEY:-}"
GROK_KEY="${GROK_KEY:-}"
PERPLEXITY_API_KEY="${PERPLEXITY_API_KEY:-}"

ALLOW_PARTIAL="false"
NON_INTERACTIVE="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --vault-name)
      require_option_value "--vault-name" "${2:-}"
      KEY_VAULT_NAME="$2"
      shift 2
      ;;
    --discord-token)
      require_option_value "--discord-token" "${2:-}"
      DISCORD_TOKEN="$2"
      shift 2
      ;;
    --gemini-key)
      require_option_value "--gemini-key" "${2:-}"
      GEMINI_KEY="$2"
      shift 2
      ;;
    --grok-key)
      require_option_value "--grok-key" "${2:-}"
      GROK_KEY="$2"
      shift 2
      ;;
    --perplexity-key)
      require_option_value "--perplexity-key" "${2:-}"
      PERPLEXITY_API_KEY="$2"
      shift 2
      ;;
    --allow-partial)
      ALLOW_PARTIAL="true"
      shift
      ;;
    --non-interactive)
      NON_INTERACTIVE="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

KEY_VAULT_NAME="$(read_value_if_missing "Key Vault name" "$KEY_VAULT_NAME")"

if [[ -z "$KEY_VAULT_NAME" ]]; then
  echo "Missing Key Vault name. Set KEY_VAULT_NAME or pass --vault-name." >&2
  exit 1
fi

require_command az

if ! az account show >/dev/null 2>&1; then
  echo "Azure CLI is not logged in. Run: az login" >&2
  exit 1
fi

if ! az keyvault show --name "$KEY_VAULT_NAME" --query id --output tsv >/dev/null; then
  echo "Unable to access Key Vault '$KEY_VAULT_NAME'." >&2
  exit 1
fi

DISCORD_TOKEN="$(read_secret_if_missing "DiscordToken" "$DISCORD_TOKEN")"
GEMINI_KEY="$(read_secret_if_missing "GeminiKey" "$GEMINI_KEY")"
GROK_KEY="$(read_secret_if_missing "GrokKey" "$GROK_KEY")"
PERPLEXITY_API_KEY="$(read_secret_if_missing "PerplexityApiKey" "$PERPLEXITY_API_KEY")"

missing_keys=()
keys_to_update=()
values_to_update=()

append_secret_if_present() {
  local key="$1"
  local value="$2"

  if [[ -z "$value" ]]; then
    missing_keys+=("$key")
  else
    keys_to_update+=("$key")
    values_to_update+=("$value")
  fi
}

append_secret_if_present "DiscordToken" "$DISCORD_TOKEN"
append_secret_if_present "GeminiKey" "$GEMINI_KEY"
append_secret_if_present "GrokKey" "$GROK_KEY"
append_secret_if_present "PerplexityApiKey" "$PERPLEXITY_API_KEY"

if [[ "$ALLOW_PARTIAL" != "true" && ${#missing_keys[@]} -gt 0 ]]; then
  echo "Refusing partial update. Missing values for: ${missing_keys[*]}" >&2
  echo "Provide all values or pass --allow-partial intentionally." >&2
  exit 1
fi

if [[ ${#keys_to_update[@]} -eq 0 ]]; then
  echo "No values provided to update." >&2
  exit 1
fi

for i in "${!keys_to_update[@]}"; do
  key="${keys_to_update[$i]}"
  value="${values_to_update[$i]}"

  az keyvault secret set \
    --vault-name "$KEY_VAULT_NAME" \
    --name "$key" \
    --value "$value" \
    --only-show-errors \
    --output none
done

echo "Updated secrets in '$KEY_VAULT_NAME': ${keys_to_update[*]}"
