variable "application" {
  type        = string
  description = "Application name"
}

variable "location_short" {
  type        = string
  description = "Short name for the Azure region to deploy to"
}

variable "location" {
  type        = string
  description = "Azure region to deploy to"
}

variable "environment" {
  type        = string
  description = "Environment (staging / prd)"
}

variable "required_secret_names" {
  type        = list(string)
  description = "Required runtime secret keys expected in Key Vault and mapped into the Container App."
  default     = ["DiscordToken", "GeminiKey", "GrokKey", "OpenRouterApiKey", "TavilyApiKey"]

  validation {
    condition     = length(var.required_secret_names) > 0
    error_message = "required_secret_names must contain at least one key."
  }

  validation {
    condition     = length(distinct(var.required_secret_names)) == length(var.required_secret_names)
    error_message = "required_secret_names must not contain duplicate keys."
  }

  validation {
    condition     = alltrue([for secret_name in var.required_secret_names : trimspace(secret_name) != ""])
    error_message = "required_secret_names must not contain empty keys."
  }
}

variable "enforce_required_secret_presence" {
  type        = bool
  description = "When true, Terraform fails fast if required Key Vault secrets are missing or disabled."
  default     = true
}
