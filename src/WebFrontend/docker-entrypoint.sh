#!/bin/sh
set -e

# Replace appsettings.json values with environment variables at runtime
CONFIG_FILE="/usr/share/nginx/html/appsettings.json"

# Function to replace JSON value using sed
replace_config() {
    local key=$1
    local value=$2
    if [ -n "$value" ]; then
        # Escape special characters in the value for sed
        escaped_value=$(echo "$value" | sed 's/[[\.*^$()+?{|]/\\&/g')
        # Replace the value in appsettings.json
        sed -i "s|\"$key\"\\s*:\\s*\"[^\"]*\"|\"$key\": \"$escaped_value\"|g" "$CONFIG_FILE"
        echo "Replaced $key with value from environment variable"
    fi
}

# Replace Api:BaseUrl if API_BASE_URL env var is set
if [ -n "$API_BASE_URL" ]; then
    # For nested JSON keys like "Api": { "BaseUrl": "..." }
    # We need to replace the BaseUrl value within the Api object
    escaped_value=$(echo "$API_BASE_URL" | sed 's/[[\.*^$()+?{|]/\\&/g')
    sed -i "s|\"BaseUrl\"\\s*:\\s*\"[^\"]*\"|\"BaseUrl\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced Api:BaseUrl with $API_BASE_URL"
fi

# Replace OAuth:Google:ClientId if OAUTH_GOOGLE_CLIENT_ID env var is set
if [ -n "$OAUTH_GOOGLE_CLIENT_ID" ]; then
    escaped_value=$(echo "$OAUTH_GOOGLE_CLIENT_ID" | sed 's/[[\.*^$()+?{|]/\\&/g')
    # Replace within the Google object
    sed -i "/\"Google\":\\s*{/,/}/ s|\"ClientId\"\\s*:\\s*\"[^\"]*\"|\"ClientId\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced OAuth:Google:ClientId with value from environment"
fi

# Replace OAuth:Microsoft:ClientId if OAUTH_MICROSOFT_CLIENT_ID env var is set
if [ -n "$OAUTH_MICROSOFT_CLIENT_ID" ]; then
    escaped_value=$(echo "$OAUTH_MICROSOFT_CLIENT_ID" | sed 's/[[\.*^$()+?{|]/\\&/g')
    # Replace within the Microsoft object
    sed -i "/\"Microsoft\":\\s*{/,/}/ s|\"ClientId\"\\s*:\\s*\"[^\"]*\"|\"ClientId\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced OAuth:Microsoft:ClientId with value from environment"
fi

# Replace OAuth:Microsoft:TenantId if OAUTH_MICROSOFT_TENANT_ID env var is set
if [ -n "$OAUTH_MICROSOFT_TENANT_ID" ]; then
    escaped_value=$(echo "$OAUTH_MICROSOFT_TENANT_ID" | sed 's/[[\.*^$()+?{|]/\\&/g')
    # Replace within the Microsoft object
    sed -i "/\"Microsoft\":\\s*{/,/}/ s|\"TenantId\"\\s*:\\s*\"[^\"]*\"|\"TenantId\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced OAuth:Microsoft:TenantId with value from environment"
fi

# Replace OAuth:GitHub:ClientId if OAUTH_GITHUB_CLIENT_ID env var is set
if [ -n "$OAUTH_GITHUB_CLIENT_ID" ]; then
    escaped_value=$(echo "$OAUTH_GITHUB_CLIENT_ID" | sed 's/[[\.*^$()+?{|]/\\&/g')
    # Replace within the GitHub object
    sed -i "/\"GitHub\":\\s*{/,/}/ s|\"ClientId\"\\s*:\\s*\"[^\"]*\"|\"ClientId\": \"$escaped_value\"|g" "$CONFIG_FILE"
    echo "Replaced OAuth:GitHub:ClientId with value from environment"
fi

# Start nginx
exec nginx -g 'daemon off;'

