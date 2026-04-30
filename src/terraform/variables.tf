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

variable "name_prefix" {
  type        = string
  description = "Used for prefixing resource names"
}

variable "name_prefix_no_dash" {
  type        = string
  description = "Name prefix but with no dash"
}