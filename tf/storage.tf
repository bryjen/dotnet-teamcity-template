# Artifact Registry Repository for Docker images
resource "google_artifact_registry_repository" "docker" {
  location      = var.gcp_region
  repository_id = "${var.project_name}-docker"
  description   = "Docker repository for ${var.project_name}"
  format        = "DOCKER"
}
