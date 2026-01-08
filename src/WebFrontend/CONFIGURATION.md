# Blazor WebAssembly Runtime Configuration

## Problem

Unlike ASP.NET Core WebAPI, Blazor WebAssembly runs in the browser and cannot directly access server-side environment variables. The `appsettings.json` file is a static file served to the browser, so traditional environment variable replacement (like `ConnectionStrings__DefaultConnection`) doesn't work at runtime.

## Solution

We use a **runtime entrypoint script** that replaces values in `appsettings.json` when the container starts, before nginx serves the files. This allows you to:

- ✅ Change configuration without rebuilding the Docker image
- ✅ Use environment variables just like WebAPI
- ✅ Deploy the same image to different environments with different configs

## How It Works

1. **Entrypoint Script** (`docker-entrypoint.sh`): Runs when the container starts
2. **Environment Variable**: Set `API_BASE_URL` as an environment variable
3. **Runtime Replacement**: Script uses `sed` to replace values in `appsettings.json`
4. **Nginx Starts**: After replacement, nginx serves the updated config file

## Usage

### Docker Compose

```yaml
webfrontend:
  environment:
    API_BASE_URL: "http://localhost:8080"
```

### Cloud Run (Terraform)

The Terraform configuration automatically sets `API_BASE_URL` to the WebAPI service URL:

```hcl
env {
  name  = "API_BASE_URL"
  value = google_cloud_run_service.webapi.status[0].url
}
```

### Manual Docker Run

```bash
docker run -e API_BASE_URL="https://api.example.com" my-frontend-image
```

## Adding More Configuration Values

To add more environment variables, edit `docker-entrypoint.sh`:

```bash
# Example: Add another config value
if [ -n "$OTHER_CONFIG_VALUE" ]; then
    escaped_value=$(echo "$OTHER_CONFIG_VALUE" | sed 's/[[\.*^$()+?{|]/\\&/g')
    sed -i "s|\"OtherKey\"\\s*:\\s*\"[^\"]*\"|\"OtherKey\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced OtherKey with $OTHER_CONFIG_VALUE"
fi
```

## Important Notes

- The replacement happens **at container startup**, not at build time
- The `appsettings.json` file in `wwwroot` is modified in-place
- Make sure the environment variable is set before the container starts
- The script preserves the JSON structure and only replaces values

## Fallback Behavior

If `API_BASE_URL` is not set, the script will skip replacement and use the default value from `appsettings.json` (useful for local development).

