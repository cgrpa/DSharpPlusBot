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

  // bootstrap - pipeline owns image
  template {
    container {
      name   = local.name_prefix
      image  = "mcr.microsoft.com/k8se/quickstart:latest"
      cpu    = 0.25
      memory = "0.5Gi"
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]
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
