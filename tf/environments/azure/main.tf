########################################
# Azure Environment Configuration
########################################
# This file calls the Azure module with all required variables

module "azure_deployment" {
  source = "../../modules/azure"

  # Azure-specific variables
  azure_location            = var.azure_location
  azure_resource_group_name = var.azure_resource_group_name
  acr_name                  = var.acr_name
  create_resource_group     = var.create_resource_group

  # Shared application variables
  environment  = var.environment
  project_name = var.project_name

  # Container images
  webapi_image      = var.webapi_image
  webfrontend_image = var.webfrontend_image

  # App configuration / secrets
  database_connection_string = var.database_connection_string
  jwt_secret                = var.jwt_secret
  cors_allowed_origins      = var.cors_allowed_origins
  api_base_url              = var.api_base_url
  frontend_base_url         = var.frontend_base_url
  email_resend_api_key      = var.email_resend_api_key
  email_resend_domain       = var.email_resend_domain

  # OAuth configuration
  oauth_google_client_id     = var.oauth_google_client_id
  oauth_microsoft_client_id = var.oauth_microsoft_client_id
  oauth_microsoft_tenant_id = var.oauth_microsoft_tenant_id
  oauth_github_client_id     = var.oauth_github_client_id
  oauth_github_client_secret = var.oauth_github_client_secret

  # Container resource configuration
  webapi_cpu              = var.webapi_cpu
  webapi_memory           = var.webapi_memory
  webfrontend_cpu         = var.webfrontend_cpu
  webfrontend_memory      = var.webfrontend_memory
  webapi_min_replicas     = var.webapi_min_replicas
  webapi_max_replicas     = var.webapi_max_replicas
  webfrontend_min_replicas = var.webfrontend_min_replicas
  webfrontend_max_replicas = var.webfrontend_max_replicas
  webapi_timeout          = var.webapi_timeout
  webfrontend_timeout     = var.webfrontend_timeout
  container_concurrency   = var.container_concurrency
}

########################################
# Outputs (pass through from module)
########################################

output "webapi_url" {
  description = "URL of the WebApi service"
  value       = module.azure_deployment.webapi_url
}

output "webfrontend_url" {
  description = "URL of the WebFrontend service"
  value       = module.azure_deployment.webfrontend_url
}

output "webapi_cors_env_var" {
  description = "Debug: CORS environment variable value"
  value       = module.azure_deployment.webapi_cors_env_var
}
