output "webapi_url" {
  description = "URL of the WebApi Cloud Run service"
  value       = google_cloud_run_service.webapi.status[0].url
}

output "webfrontend_url" {
  description = "URL of the WebFrontend Cloud Run service"
  value       = google_cloud_run_service.webfrontend.status[0].url
}

output "artifact_registry_repository" {
  description = "Artifact Registry repository for Docker images"
  value       = google_artifact_registry_repository.docker.name
}

output "docker_repository_url" {
  description = "Full Docker repository URL"
  value       = "${var.gcp_region}-docker.pkg.dev/${var.gcp_project_id}/${google_artifact_registry_repository.docker.repository_id}"
}

