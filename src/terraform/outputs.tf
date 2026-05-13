output "resource_group_name" {
  value = azurerm_resource_group.this.name
}

output "container_app_name" {
  value = azurerm_container_app.this.name
}

output "container_app_environment_name" {
  value = azurerm_container_app_environment.this.name
}

output "container_registry_login_server" {
  value = azurerm_container_registry.this.login_server
}

output "container_app_id" {
  value = azurerm_container_app.this.id
}

output "container_registry_id" {
  value = azurerm_container_registry.this.id
}

output "key_vault_name" {
  value = azurerm_key_vault.this.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}
