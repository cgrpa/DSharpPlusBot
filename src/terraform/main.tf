resource "azurerm_resource_group" "this" {
    name     = "${local.name_prefix}-rg"
    location = var.location
    tags     = local.common_tags
}