# CORS Environment Variable Pipeline Debugging

## Pipeline Flow

1. **CI/CD Pipeline** sets environment variable:
   ```
   TF_VAR_cors_allowed_origins=https://personal-webfrontend-keemudndda-nn.a.run.app
   ```

2. **Terraform** reads the variable:
   - Terraform automatically reads `TF_VAR_*` environment variables
   - Maps to: `var.cors_allowed_origins`
   - Variable definition: `tf/cloudrun/variables.tf`

3. **Terraform** sets Cloud Run environment variable:
   - Resource: `google_cloud_run_service.webapi`
   - Location: `tf/cloudrun/compute.tf` lines 55-62
   - Sets: `Cors__AllowedOrigins__0` = `var.cors_allowed_origins`

4. **Cloud Run Container** should have:
   - Environment variable: `Cors__AllowedOrigins__0=https://personal-webfrontend-keemudndda-nn.a.run.app`

5. **ASP.NET Core** reads configuration:
   - Checks: `Cors:AllowedOrigins:0` (mapped from `Cors__AllowedOrigins__0`)
   - Code: `src/WebApi/Configuration/ServiceConfiguration.cs`

## Debugging Steps

### 1. Verify Terraform Variable is Set
After Terraform runs, check the output:
```bash
terraform output webapi_cors_env_var
```
This should show: `Cors__AllowedOrigins__0=https://personal-webfrontend-keemudndda-nn.a.run.app`

### 2. Verify Cloud Run Service Configuration
Check the Cloud Run service directly:
```bash
gcloud run services describe personal-webapi --region=us-central1 --format="value(spec.template.spec.containers[0].env)"
```

Or check in GCP Console:
- Go to Cloud Run → your-webapi service → REVISIONS → click on revision → CONTAINER → Environment variables

### 3. Check API Debug Endpoint
Call: `GET https://your-api-url/api/config/cors`

Look for:
- `environmentVariable`: Should show the URL, not "(not set)"
- `allCorsEnvironmentVariables`: Should list `Cors__AllowedOrigins__0` with the value
- `corsAllowedOriginsUnderscore0`: Should show the URL, not "(null)"

## Common Issues

### Issue 1: Terraform Variable Not Read
**Symptom**: `terraform output webapi_cors_env_var` shows "CORS env var not set"

**Cause**: `TF_VAR_cors_allowed_origins` not set when Terraform runs

**Fix**: Ensure CI/CD sets the environment variable before running `terraform apply`

### Issue 2: Terraform Didn't Update Service
**Symptom**: Terraform output shows correct value, but Cloud Run doesn't have it

**Cause**: Terraform apply didn't detect a change, or service wasn't updated

**Fix**: 
```bash
terraform apply -refresh=true
# Or force update:
terraform taint google_cloud_run_service.webapi
terraform apply
```

### Issue 3: Environment Variable Name Mismatch
**Symptom**: Cloud Run has the env var, but API doesn't see it

**Cause**: Environment variable name doesn't match what ASP.NET Core expects

**Fix**: Verify the name is exactly `Cors__AllowedOrigins__0` (double underscores)

### Issue 4: Cloud Run Revision Not Updated
**Symptom**: Old revision is still running with old configuration

**Cause**: Cloud Run is using a cached revision

**Fix**: Force a new revision by updating the service:
```bash
gcloud run services update personal-webapi --region=us-central1 --update-env-vars Cors__AllowedOrigins__0=https://personal-webfrontend-keemudndda-nn.a.run.app
```

## Verification Checklist

- [ ] CI/CD sets `TF_VAR_cors_allowed_origins` before Terraform runs
- [ ] Terraform reads the variable (check `terraform plan` output)
- [ ] Terraform applies the change (check `terraform apply` output)
- [ ] Cloud Run service has the environment variable (check GCP Console or gcloud)
- [ ] Cloud Run is using the latest revision
- [ ] API debug endpoint shows the environment variable

