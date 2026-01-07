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
            cpu    = "1"
            memory = "1Gi"
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
        "autoscaling.knative.dev/maxScale" = "10"
        "autoscaling.knative.dev/minScale" = "1"
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
            cpu    = "1"
            memory = "512Mi"
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment == "prod" ? "Production" : "Development"
        }

        # API_BASE_URL is typically baked into the frontend image, but you could
        # also pass it as an env var here once the WebApi URL is known.
      }

      container_concurrency = 80
      timeout_seconds       = 300
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "10"
        "autoscaling.knative.dev/minScale" = "1"
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
