resource "azurerm_container_registry" "this" {
    name                = "${local.name_prefix_no_dash}acr"
    resource_group_name = azurerm_resource_group.this.name
    location            = var.location
    sku                 = "Basic"
    admin_enabled       = false
    tags                = local.common_tags
    
    identity {
        type = "SystemAssigned"
    }
}

resource "azurerm_role_assignment" "acr_push" {
    scope = azurerm_container_registry.this.id
    role_definition_name = "AcrPush"
    principal_id = data.azurerm_client_config.this.object_id
}

resource "azurerm_role_assignment" "acr_pull" {
    scope = azurerm_container_registry.this.id
    role_definition_name = "AcrPull"
    principal_id = azurerm_container_app.this.identity[0].principal_id
}

