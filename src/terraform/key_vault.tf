resource "azurerm_key_vault" "this" {
    name = "${local.name_prefix}-kv"
    resource_group_name = azurerm_resource_group.this.name
    location = var.location
    tenant_id = data.azurerm_client_config.this.tenant_id
    purge_protection_enabled = false
    sku_name = "standard"
    enable_rbac_authorization   = true

    tags = local.common_tags
}

resource "azurerm_role_assignment" "kv_pipeline_access" {
    scope = azurerm_key_vault.this.id
    role_definition_name = "Key Vault Secrets User"
    principal_id = data.azurerm_client_config.this.object_id
}