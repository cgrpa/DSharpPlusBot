resource "azurerm_storage_account" "this" {
  name                       = "${local.name_prefix_no_dash}sa"
  resource_group_name        = azurerm_resource_group.this.name
  location                   = var.location
  account_tier               = "Standard"
  account_replication_type   = "LRS"
  tags                       = local.common_tags
  shared_access_key_enabled  = false
  https_traffic_only_enabled = true

}

resource "azurerm_storage_account" "image_generation" {
  name                             = "${local.name_prefix_no_dash}imgsa"
  resource_group_name              = azurerm_resource_group.this.name
  location                         = var.location
  account_tier                     = "Standard"
  account_replication_type         = "LRS"
  tags                             = local.common_tags
  shared_access_key_enabled        = false
  https_traffic_only_enabled       = true
  min_tls_version                  = "TLS1_2"
  allow_nested_items_to_be_public  = true
  cross_tenant_replication_enabled = false
}

resource "azurerm_storage_container" "generated_images" {
  name                  = "generated-images"
  storage_account_id    = azurerm_storage_account.image_generation.id
  container_access_type = "blob"
}

resource "azurerm_storage_management_policy" "image_generation_retention" {
  storage_account_id = azurerm_storage_account.image_generation.id

  rule {
    name    = "delete-generated-images-after-seven-days"
    enabled = true

    filters {
      prefix_match = [azurerm_storage_container.generated_images.name]
      blob_types   = ["blockBlob"]
    }

    actions {
      base_blob {
        delete_after_days_since_creation_greater_than = 7
      }
    }
  }
}

resource "azurerm_role_assignment" "image_generation_blob_contributor" {
  scope                = azurerm_storage_account.image_generation.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_container_app.this.identity[0].principal_id
}

resource "azurerm_role_assignment" "image_generation_table_contributor" {
  scope                = azurerm_storage_account.image_generation.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_container_app.this.identity[0].principal_id
}
