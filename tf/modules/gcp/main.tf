########################################
# Cloud Run Service: WebApi
########################################

resource "google_cloud_run_service" "webapi" {
  name     = "${var.project_name}-webapi"
  location = local.cloud_run_location

  lifecycle {
    prevent_destroy = true
  }

  template {
    spec {
      containers {
        image = var.webapi_image != "" ? var.webapi_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webapi:latest"

        ports {
          container_port = local.shared_config.webapi_config.port
        }

        resources {
          limits = {
            cpu    = local.shared_config.webapi_config.cpu
            memory = local.shared_config.webapi_config.memory
          }
        }

        # Static environment variables
        dynamic "env" {
          for_each = local.webapi_env_vars_all
          content {
            name  = env.key
            value = env.value
          }
        }
      }

      container_concurrency = local.shared_config.webapi_config.concurrency
      timeout_seconds      = local.shared_config.webapi_config.timeout
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = tostring(local.shared_config.webapi_config.max_replicas)
        "autoscaling.knative.dev/minScale" = tostring(local.shared_config.webapi_config.min_replicas)
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

  lifecycle {
    prevent_destroy = true
  }

  template {
    spec {
      containers {
        image = var.webfrontend_image != "" ? var.webfrontend_image : "gcr.io/${var.gcp_project_id}/${var.project_name}-webfrontend:latest"

        ports {
          container_port = local.shared_config.webfrontend_config.port
        }

        resources {
          limits = {
            cpu    = local.shared_config.webfrontend_config.cpu
            memory = local.shared_config.webfrontend_config.memory
          }
        }

        # Static environment variables
        dynamic "env" {
          for_each = local.webfrontend_env_vars_all
          content {
            name  = env.key
            value = env.value
          }
        }

        # API_BASE_URL - dynamically resolved from WebApi service URL if not provided
        env {
          name  = "API_BASE_URL"
          value = var.api_base_url != "" ? var.api_base_url : google_cloud_run_service.webapi.status[0].url
        }
      }

      container_concurrency = local.shared_config.webfrontend_config.concurrency
      timeout_seconds      = local.shared_config.webfrontend_config.timeout
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = tostring(local.shared_config.webfrontend_config.max_replicas)
        "autoscaling.knative.dev/minScale" = tostring(local.shared_config.webfrontend_config.min_replicas)
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
