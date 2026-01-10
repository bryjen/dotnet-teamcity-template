########################################
# Cloud Run Service: WebApi
########################################

resource "google_cloud_run_service" "webapi" {
  name     = "${var.project_name}-webapi"
  location = local.cloud_run_location

  template {
    spec {
      containers {
        image = var.webapi_image != "" ? var.webapi_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webapi:latest"

        ports {
          container_port = 8080
        }

        resources {
          limits = {
            cpu    = "0.5"   # Reduced from 1 to 0.5 vCPU for cost savings
            memory = "512Mi" # Reduced from 1Gi to 512Mi (max for 0.5 vCPU)
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment == "prod" ? "Production" : "Development"
        }

        env {
          name  = "OTEL_SERVICE_NAME"
          value = "WebApi"
        }

        # Database connection string
        dynamic "env" {
          for_each = var.database_connection_string != "" ? [1] : []
          content {
            name  = "ConnectionStrings__DefaultConnection"
            value = var.database_connection_string
          }
        }

        # JWT Secret
        dynamic "env" {
          for_each = var.jwt_secret != "" ? [1] : []
          content {
            name  = "Jwt__Secret"
            value = var.jwt_secret
          }
        }

        # CORS - allow frontend URL
        # Set as a single string value (not array index)
        dynamic "env" {
          for_each = var.cors_allowed_origins != "" ? [1] : []
          content {
            name  = "Cors__AllowedOrigins"
            value = var.cors_allowed_origins
          }
        }
      }

      container_concurrency = 1  # Must be 1 when using < 1 vCPU
      timeout_seconds       = 60 # Reduced from 300s to 60s for cost savings
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "2" # Reduced from 10 to 2 for dev/test
        "autoscaling.knative.dev/minScale" = "0" # Scale to zero when idle (saves money)
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

########################################
# Cloud Run Service: WebFrontend
########################################

resource "google_cloud_run_service" "webfrontend" {
  name     = "${var.project_name}-webfrontend"
  location = local.cloud_run_location

  template {
    spec {
      containers {
        image = var.webfrontend_image != "" ? var.webfrontend_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webfrontend:latest"

        ports {
          container_port = 8080
        }

        resources {
          limits = {
            cpu    = "0.5"   # Reduced from 1 to 0.5 vCPU for cost savings
            memory = "256Mi" # Reduced from 512Mi to 256Mi (sufficient for static frontend)
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment == "prod" ? "Production" : "Development"
        }

        # API_BASE_URL - passed as env var and replaced at runtime in appsettings.json
        # The docker-entrypoint.sh script will replace the value in appsettings.json
        # before nginx starts. This allows runtime configuration without rebuilding.
        # If api_base_url variable is set, use it; otherwise auto-detect from WebAPI service URL
        env {
          name  = "API_BASE_URL"
          value = var.api_base_url != "" ? var.api_base_url : google_cloud_run_service.webapi.status[0].url
        }
      }

      container_concurrency = 1  # Must be 1 when using < 1 vCPU
      timeout_seconds       = 60 # Reduced from 300s to 60s for cost savings
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "2" # Reduced from 10 to 2 for dev/test
        "autoscaling.knative.dev/minScale" = "0" # Scale to zero when idle (saves money)
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

########################################
# IAM: Public access for both services
########################################

resource "google_cloud_run_service_iam_member" "webapi_public" {
  service  = google_cloud_run_service.webapi.name
  location = google_cloud_run_service.webapi.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

resource "google_cloud_run_service_iam_member" "webfrontend_public" {
  service  = google_cloud_run_service.webfrontend.name
  location = google_cloud_run_service.webfrontend.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}
