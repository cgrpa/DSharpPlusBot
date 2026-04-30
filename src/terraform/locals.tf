locals {
    common_tags = {
        Environment = upper(var.environment)
        Application = var.application
        Location = var.location
    }
    name_prefix = "${var.environment}-${var.application}"
    name_prefix_no_dash = "${var.environment}${var.application}"
}