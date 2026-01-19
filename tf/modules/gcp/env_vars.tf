########################################
# GCP Environment Variable Helpers
########################################
# Converts shared module env_vars maps to GCP Cloud Run env {} block format

locals {
  # Import shared module configuration
  shared_config = {
    webapi_config         = {
      cpu            = var.webapi_cpu
      memory         = var.webapi_memory
      port           = 8080
      min_replicas   = var.webapi_min_replicas
      max_replicas   = var.webapi_max_replicas
      timeout        = var.webapi_timeout
      concurrency    = var.container_concurrency
    }
    webfrontend_config    = {
      cpu            = var.webfrontend_cpu
      memory         = var.webfrontend_memory
      port           = 8080
      min_replicas   = var.webfrontend_min_replicas
      max_replicas   = var.webfrontend_max_replicas
      timeout        = var.webfrontend_timeout
      concurrency    = var.container_concurrency
    }
    webapi_env_vars       = {
      "ASPNETCORE_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Development"
      "OTEL_SERVICE_NAME"      = "WebApi"
    }
    webapi_env_vars_conditional = {
      "ConnectionStrings__DefaultConnection" = var.database_connection_string
      "Jwt__Secret"                         = var.jwt_secret
      "Cors__AllowedOrigins"                = var.cors_allowed_origins
      "Frontend__BaseUrl"                   = var.frontend_base_url
      "Email__Resend__ApiKey"               = var.email_resend_api_key
      "Email__Resend__Domain"                = var.email_resend_domain
      "OAuth__Google__ClientId"              = var.oauth_google_client_id
      "OAuth__Microsoft__ClientId"          = var.oauth_microsoft_client_id
      "OAuth__Microsoft__TenantId"          = var.oauth_microsoft_tenant_id
      "OAuth__GitHub__ClientId"             = var.oauth_github_client_id
      "OAuth__GitHub__ClientSecret"         = var.oauth_github_client_secret
    }
    webfrontend_env_vars  = {
      "ASPNETCORE_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Development"
    }
    webfrontend_env_vars_conditional = {
      "OAUTH_GOOGLE_CLIENT_ID"    = var.oauth_google_client_id
      "OAUTH_MICROSOFT_CLIENT_ID" = var.oauth_microsoft_client_id
      "OAUTH_MICROSOFT_TENANT_ID" = var.oauth_microsoft_tenant_id
      "OAUTH_GITHUB_CLIENT_ID"    = var.oauth_github_client_id
    }
  }

  # Merge and filter empty values for WebApi
  webapi_env_vars_all = merge(
    local.shared_config.webapi_env_vars,
    {
      for k, v in local.shared_config.webapi_env_vars_conditional : k => v
      if v != "" && v != null
    }
  )

  # Merge and filter empty values for WebFrontend
  # Note: API_BASE_URL will be set dynamically in main.tf
  webfrontend_env_vars_all = merge(
    local.shared_config.webfrontend_env_vars,
    {
      for k, v in local.shared_config.webfrontend_env_vars_conditional : k => v
      if v != "" && v != null
    }
  )
}
