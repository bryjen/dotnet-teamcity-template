########################################
# Environment Variable Definitions
########################################
# These maps define all environment variables for both services.
# Provider-specific modules will convert these to their native format.

locals {
  # WebApi environment variables
  webapi_env_vars = {
    "ASPNETCORE_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Development"
    "OTEL_SERVICE_NAME"      = "WebApi"
  }

  # WebApi environment variables (conditional - only included if values are provided)
  webapi_env_vars_conditional = {
    "ConnectionStrings__DefaultConnection" = var.database_connection_string
    "Jwt__Secret"                         = var.jwt_secret
    "Cors__Enabled"                       = tostring(var.cors_enabled)
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

  # WebFrontend environment variables
  webfrontend_env_vars = {
    "ASPNETCORE_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Development"
    # API_BASE_URL will be set by provider modules (may reference service URL)
  }

  # WebFrontend environment variables (conditional - only included if values are provided)
  webfrontend_env_vars_conditional = {
    "OAUTH_GOOGLE_CLIENT_ID"    = var.oauth_google_client_id
    "OAUTH_MICROSOFT_CLIENT_ID" = var.oauth_microsoft_client_id
    "OAUTH_MICROSOFT_TENANT_ID" = var.oauth_microsoft_tenant_id
    "OAUTH_GITHUB_CLIENT_ID"    = var.oauth_github_client_id
  }

  # Helper function to merge and filter empty values
  # Provider modules should use this pattern to filter out empty strings
  webapi_env_vars_all = merge(
    local.webapi_env_vars,
    {
      for k, v in local.webapi_env_vars_conditional : k => v
      if v != "" && v != null
    }
  )

  webfrontend_env_vars_all = merge(
    local.webfrontend_env_vars,
    {
      for k, v in local.webfrontend_env_vars_conditional : k => v
      if v != "" && v != null
    }
  )
}
