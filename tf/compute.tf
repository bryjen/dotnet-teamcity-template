# Cloud Run Service for WebApi
resource "google_cloud_run_service" "webapi" {
  name     = "${var.project_name}-webapi"
  location = var.gcp_region

  template {
    spec {
      containers {
        image = var.webapi_image != "" ? var.webapi_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webapi:latest"

        ports {
          container_port = var.webapi_port
        }

        resources {
          limits = {
            cpu    = var.webapi_cpu
            memory = var.webapi_memory
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
        dynamic "env" {
          for_each = var.cors_allowed_origins != "" ? [1] : []
          content {
            name  = "Cors__AllowedOrigins__0"
            value = var.cors_allowed_origins
          }
        }
      }

      container_concurrency = 80
      timeout_seconds       = 300
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = tostring(var.webapi_max_instances)
        "autoscaling.knative.dev/minScale" = tostring(var.webapi_min_instances)
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

# Cloud Run Service for WebFrontend
resource "google_cloud_run_service" "webfrontend" {
  name     = "${var.project_name}-webfrontend"
  location = var.gcp_region

  template {
    spec {
      containers {
        image = var.webfrontend_image != "" ? var.webfrontend_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webfrontend:latest"

        ports {
          container_port = var.webfrontend_port
        }

        resources {
          limits = {
            cpu    = var.webfrontend_cpu
            memory = var.webfrontend_memory
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment == "prod" ? "Production" : "Development"
        }

        # API_BASE_URL - set via build arg or update after deployment
        # The image should be built with the correct API_BASE_URL
        # Or you can set it here once webapi URL is known
      }

      container_concurrency = 80
      timeout_seconds       = 300
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = tostring(var.webfrontend_max_instances)
        "autoscaling.knative.dev/minScale" = tostring(var.webfrontend_min_instances)
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

# IAM Policy to allow unauthenticated access to WebApi
resource "google_cloud_run_service_iam_member" "webapi_public" {
  service  = google_cloud_run_service.webapi.name
  location = google_cloud_run_service.webapi.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# IAM Policy to allow unauthenticated access to WebFrontend
resource "google_cloud_run_service_iam_member" "webfrontend_public" {
  service  = google_cloud_run_service.webfrontend.name
  location = google_cloud_run_service.webfrontend.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}
