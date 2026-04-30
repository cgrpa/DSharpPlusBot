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