########################################
# Common Application Variables
########################################

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
  default     = "YOUR_PROJECT_NAME"
}

########################################
# Container Images
########################################

variable "webapi_image" {
  description = "Docker image for WebApi (full image path)"
  type        = string
  default     = ""
}

variable "webfrontend_image" {
  description = "Docker image for WebFrontend (full image path)"
  type        = string
  default     = ""
}

########################################
# App configuration / secrets
########################################

variable "database_connection_string" {
  description = "Database connection string"
  type        = string
  sensitive   = true
  default     = ""
}

variable "jwt_secret" {
  description = "JWT secret key"
  type        = string
  sensitive   = true
  default     = ""
}

variable "cors_enabled" {
  description = "Enable or disable CORS. Set to false to completely disable CORS."
  type        = bool
  default     = true
}

variable "cors_allowed_origins" {
  description = "CORS allowed origins (single origin, or comma-separated)"
  type        = string
  default     = ""
}

variable "api_base_url" {
  description = "API base URL for frontend configuration. If not set, automatically uses the WebAPI service URL."
  type        = string
  default     = ""
}

variable "frontend_base_url" {
  description = "Frontend base URL (used for password reset links and other frontend redirects)"
  type        = string
  default     = ""
}

variable "email_resend_api_key" {
  description = "Resend API key for email service"
  type        = string
  sensitive   = true
  default     = ""
}

variable "email_resend_domain" {
  description = "Resend domain for email service"
  type        = string
  default     = ""
}

########################################
# OAuth configuration
########################################

variable "oauth_google_client_id" {
  description = "Google OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_microsoft_client_id" {
  description = "Microsoft OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_microsoft_tenant_id" {
  description = "Microsoft OAuth Tenant ID (default: 'common')"
  type        = string
  sensitive   = false
  default     = "common"
}

variable "oauth_github_client_id" {
  description = "GitHub OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_github_client_secret" {
  description = "GitHub OAuth Client Secret"
  type        = string
  sensitive   = true
  default     = ""
}

########################################
# Container Resource Configuration
########################################

variable "webapi_cpu" {
  description = "CPU allocation for WebApi container (e.g., '0.5', '1', '2')"
  type        = string
  default     = "0.5"
}

variable "webapi_memory" {
  description = "Memory allocation for WebApi container (e.g., '512Mi', '1Gi')"
  type        = string
  default     = "512Mi"
}

variable "webfrontend_cpu" {
  description = "CPU allocation for WebFrontend container (e.g., '0.5', '1', '2')"
  type        = string
  default     = "0.5"
}

variable "webfrontend_memory" {
  description = "Memory allocation for WebFrontend container (e.g., '256Mi', '512Mi')"
  type        = string
  default     = "256Mi"
}

variable "webapi_min_replicas" {
  description = "Minimum number of replicas for WebApi"
  type        = number
  default     = 0
}

variable "webapi_max_replicas" {
  description = "Maximum number of replicas for WebApi"
  type        = number
  default     = 2
}

variable "webfrontend_min_replicas" {
  description = "Minimum number of replicas for WebFrontend"
  type        = number
  default     = 0
}

variable "webfrontend_max_replicas" {
  description = "Maximum number of replicas for WebFrontend"
  type        = number
  default     = 2
}

variable "webapi_timeout" {
  description = "Request timeout in seconds for WebApi"
  type        = number
  default     = 60
}

variable "webfrontend_timeout" {
  description = "Request timeout in seconds for WebFrontend"
  type        = number
  default     = 60
}

variable "container_concurrency" {
  description = "Container concurrency (must be 1 when using < 1 vCPU)"
  type        = number
  default     = 1
}
