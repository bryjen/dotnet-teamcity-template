#!/bin/sh
set -e

# Start Tailscale in the background if auth key is provided
if [ -n "$TS_AUTHKEY" ]; then
  echo "Starting Tailscale..."

  # Use /tmp for Tailscale state (writable by any user, ephemeral is fine for containers)
  TS_STATE_DIR="/tmp/tailscale"
  mkdir -p "$TS_STATE_DIR"

  # Start tailscaled in userspace networking mode (container-friendly)
  tailscaled --state="$TS_STATE_DIR/tailscaled.state" --tun=userspace-networking &

  # Wait briefly for tailscaled to come up
  sleep 2

  # Authenticate with Tailscale using only the auth key (outbound use-case)
  tailscale up --authkey="$TS_AUTHKEY"

  echo "Waiting for Tailscale to connect..."
  until tailscale status > /dev/null 2>&1; do
    sleep 1
  done

  echo "Tailscale connected:"
  tailscale status
fi

# Start the .NET application
exec dotnet WebApi.dll --urls "http://+:8080"


