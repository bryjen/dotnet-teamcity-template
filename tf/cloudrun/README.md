# Terraform Infrastructure for AspTemplate (GCP)

Simple Terraform configuration for deploying AspTemplate to Google Cloud Platform using Cloud Run.

## Architecture

- **Compute**: Cloud Run (fully managed container service with configurable CPU/memory)
- **Container Registry**: Artifact Registry for Docker images
- **Networking**: Cloud Run handles all networking automatically

## Prerequisites

1. **Google Cloud SDK** installed and configured
2. **Terraform** >= 1.0 installed
3. **Docker** installed (for building and pushing images)
4. GCP project with billing enabled
5. Enable required APIs:
   ```bash
   gcloud services enable run.googleapis.com
   gcloud services enable artifactregistry.googleapis.com
   ```

## Setup

### 1. Configure Variables

Create a `terraform.tfvars` file:

```hcl
gcp_project_id = "your-project-id"
gcp_region     = "us-central1"
environment    = "dev"
project_name   = "asptemplate"

# Instance properties for WebApi
webapi_cpu         = "1"      # 1 vCPU
webapi_memory      = "1Gi"    # 1 GB
webapi_max_instances = 10
webapi_min_instances = 1

# Instance properties for WebFrontend
webfrontend_cpu         = "1"
webfrontend_memory      = "512Mi"
webfrontend_max_instances = 10
webfrontend_min_instances = 1

# Optional: Pre-built images (leave empty to build manually)
# webapi_image = "gcr.io/your-project-id/asptemplate-webapi:latest"
# webfrontend_image = "gcr.io/your-project-id/asptemplate-webfrontend:latest"

# Secrets (optional, can be set via environment variables or Secret Manager)
# database_connection_string = "Server=...;Database=...;..."
# jwt_secret = "your-secret-key"

# CORS (set after first deployment with webfrontend URL)
# cors_allowed_origins = "https://asptemplate-webfrontend-xxxxx.run.app"
```

### 2. Initialize Terraform

```bash
cd tf
terraform init
```

### 3. Review the Plan

```bash
terraform plan
```

### 4. Apply the Configuration

```bash
terraform apply
```

## Building and Pushing Docker Images

After the infrastructure is created, build and push your Docker images:

### Authenticate with Artifact Registry

```bash
gcloud auth configure-docker ${var.gcp_region}-docker.pkg.dev
```

### Build and Push WebApi

```bash
# Get the repository URL from Terraform output
REPO_URL=$(terraform output -raw docker_repository_url)

# Build the image
docker build -f src/WebApi/Dockerfile -t $REPO_URL/webapi:latest .

# Push the image
docker push $REPO_URL/webapi:latest
```

### Build and Push WebFrontend

```bash
REPO_URL=$(terraform output -raw docker_repository_url)
WEBAPI_URL=$(terraform output -raw webapi_url)

# Build the image (from solution root) with API URL baked in
docker build -f src/WebFrontend/Dockerfile \
  -t $REPO_URL/webfrontend:latest \
  --build-arg API_BASE_URL=$WEBAPI_URL/api/ .

# Push the image
docker push $REPO_URL/webfrontend:latest
```

### Configure CORS (After First Deployment)

After deploying both services, update CORS settings:

1. Get the frontend URL:
   ```bash
   FRONTEND_URL=$(terraform output -raw webfrontend_url)
   ```

2. Update `terraform.tfvars` with the CORS origin:
   ```hcl
   cors_allowed_origins = "https://asptemplate-webfrontend-xxxxx.run.app"
   ```

3. Apply again:
   ```bash
   terraform apply
   ```

### Update Cloud Run Services

After pushing new images, update the services:

```bash
# Update WebApi
gcloud run services update asptemplate-webapi \
  --image $REPO_URL/webapi:latest \
  --region us-central1

# Update WebFrontend
gcloud run services update asptemplate-webfrontend \
  --image $REPO_URL/webfrontend:latest \
  --region us-central1
```

Or update the image in `terraform.tfvars` and run `terraform apply`.

## Configuration

### Adjusting Instance Properties

You can modify CPU and memory in `terraform.tfvars`:

- **CPU**: Valid values are "1", "2", "4", "6", "8" (vCPUs)
- **Memory**: Valid values like "128Mi", "256Mi", "512Mi", "1Gi", "2Gi", "4Gi", etc.
- **Memory limits**: Must be between 128Mi and 8Gi per vCPU

### Autoscaling

Configure min/max instances to control autoscaling:
- `min_instances`: Minimum number of instances (0 for scale-to-zero)
- `max_instances`: Maximum number of instances

## Outputs

After applying, get the service URLs:

```bash
# Get service URLs
terraform output webapi_url
terraform output webfrontend_url

# Get Docker repository URL
terraform output docker_repository_url
```

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

## Notes

- Cloud Run automatically handles HTTPS, load balancing, and scaling
- Services are publicly accessible by default (configure IAM for private access if needed)
- Container logs are automatically sent to Cloud Logging
- No VPC or networking configuration needed - Cloud Run handles it all
- For production, consider using Secret Manager for sensitive values instead of variables
