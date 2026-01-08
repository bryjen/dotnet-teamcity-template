variable "gcp_project_id" {
  description = "GCP Project ID"
  type        = string
  default     = "YOUR_PROJECT_ID"
}

variable "gcp_region" {
  description = "GCP region for resources"
  type        = string
  default     = "us-central1"
}

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
  description = "Docker image for WebApi (e.g., gcr.io/PROJECT_ID/asptemplate-webapi:latest)"
  type        = string
  default     = ""
}

variable "webfrontend_image" {
  description = "Docker image for WebFrontend (e.g., gcr.io/PROJECT_ID/asptemplate-webfrontend:latest)"
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

########################################
# Tailscale configuration
########################################

variable "tailscale_authkey" {
  description = "Tailscale auth key for outbound connections to Tailscale network"
  type        = string
  sensitive   = true
  default     = ""
}
