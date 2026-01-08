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

# You can add more replacements here as needed
# Example: if [ -n "$OTHER_CONFIG" ]; then ...

# Start nginx
exec nginx -g 'daemon off;'

