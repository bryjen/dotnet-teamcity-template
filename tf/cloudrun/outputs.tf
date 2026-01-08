output "webapi_url" {
  description = "URL of the WebApi Cloud Run service"
  value       = google_cloud_run_service.webapi.status[0].url
}

output "webfrontend_url" {
  description = "URL of the WebFrontend Cloud Run service"
  value       = google_cloud_run_service.webfrontend.status[0].url
}

