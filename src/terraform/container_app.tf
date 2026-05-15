resource "azurerm_container_app_environment" "this" {
  name                       = "${local.name_prefix}-cae"
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  tags                       = local.common_tags
}

resource "azurerm_container_app" "this" {
  name                         = "${local.name_prefix}-app"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = azurerm_resource_group.this.name
  revision_mode                = "Single"
  tags                         = local.common_tags

  // use managed identity for private ACR image pulls
  registry {
    server   = azurerm_container_registry.this.login_server
    identity = "System"
  }

  dynamic "secret" {
    for_each = local.container_app_secret_aliases

    content {
      name                = secret.value
      identity            = "System"
      key_vault_secret_id = format("%ssecrets/%s", azurerm_key_vault.this.vault_uri, secret.key)
    }
  }

  // bootstrap - pipeline owns image
  template {
    container {
      name   = local.name_prefix
      image  = "mcr.microsoft.com/k8se/quickstart:latest"
      cpu    = 0.25
      memory = "0.5Gi"

      dynamic "env" {
        for_each = local.container_app_secret_aliases

        content {
          name        = env.key
          secret_name = env.value
        }
      }

      env {
        name  = "ImageGeneration__StorageAccountName"
        value = azurerm_storage_account.image_generation.name
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]

    precondition {
      condition     = length(setsubtract(local.required_secret_keys, local.alias_map_keys)) == 0 && length(setsubtract(local.alias_map_keys, local.required_secret_keys)) == 0
      error_message = "required_secret_names must match alias-map keys exactly. Expected keys: ${join(", ", sort(keys(local.required_secret_alias_map)))}."
    }

    precondition {
      condition     = !var.enforce_required_secret_presence || length(local.missing_required_secret_names) == 0
      error_message = "Required Key Vault secrets are missing: ${join(", ", local.missing_required_secret_names)}."
    }

    precondition {
      condition     = !var.enforce_required_secret_presence || length(local.disabled_required_secret_names) == 0
      error_message = "Required Key Vault secrets are disabled: ${join(", ", local.disabled_required_secret_names)}."
    }
  }

  identity {
    type = "SystemAssigned"
  }
}

// rbac
resource "azurerm_role_assignment" "container_app_acr_pull" {
  scope                = azurerm_container_app.this.id
  role_definition_name = "Contributor"
  principal_id         = data.azurerm_client_config.this.object_id
}
