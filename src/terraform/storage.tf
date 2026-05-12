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