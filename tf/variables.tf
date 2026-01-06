variable "gcp_project_id" {
  description = "GCP Project ID"
  type        = string
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
  default     = "asptemplate"
}

# Cloud Run Configuration
variable "webapi_cpu" {
  description = "CPU allocation for WebApi service (e.g., '1', '2', '4')"
  type        = string
  default     = "1"
}

variable "webapi_memory" {
  description = "Memory allocation for WebApi service (e.g., '512Mi', '1Gi', '2Gi')"
  type        = string
  default     = "1Gi"
}

variable "webapi_max_instances" {
  description = "Maximum number of WebApi instances"
  type        = number
  default     = 10
}

variable "webapi_min_instances" {
  description = "Minimum number of WebApi instances"
  type        = number
  default     = 1
}

variable "webfrontend_cpu" {
  description = "CPU allocation for WebFrontend service"
  type        = string
  default     = "1"
}

variable "webfrontend_memory" {
  description = "Memory allocation for WebFrontend service"
  type        = string
  default     = "512Mi"
}

variable "webfrontend_max_instances" {
  description = "Maximum number of WebFrontend instances"
  type        = number
  default     = 10
}

variable "webfrontend_min_instances" {
  description = "Minimum number of WebFrontend instances"
  type        = number
  default     = 1
}

# Container Configuration
variable "webapi_image" {
  description = "Docker image for WebApi (e.g., gcr.io/PROJECT_ID/webapi:latest)"
  type        = string
  default     = ""
}

variable "webfrontend_image" {
  description = "Docker image for WebFrontend"
  type        = string
  default     = ""
}

variable "webapi_port" {
  description = "Port for WebApi container"
  type        = number
  default     = 8080
}

variable "webfrontend_port" {
  description = "Port for WebFrontend container"
  type        = number
  default     = 8080
}

# Secrets
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
  description = "CORS allowed origins (comma-separated or leave empty to set after deployment)"
  type        = string
  default     = ""
}

