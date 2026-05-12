data "azurerm_client_config" "this" {}

data "azurerm_key_vault_secrets" "required" {
  key_vault_id = azurerm_key_vault.this.id
}
