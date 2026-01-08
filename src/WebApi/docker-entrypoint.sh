#!/bin/sh
set +e  # Don't exit on error, we want to see what's happening

echo "=========================================="
echo "WebApi Container Startup"
echo "=========================================="

# Check if Tailscale auth key is provided
if [ -n "$TS_AUTHKEY" ]; then
  echo "[DEBUG] TS_AUTHKEY is SET (length: ${#TS_AUTHKEY} chars)"
  echo "[DEBUG] TS_AUTHKEY prefix: ${TS_AUTHKEY%%-*}-****"
  echo "[DEBUG] Starting Tailscale setup..."
  
  # Create all directories Tailscale needs
  echo "[DEBUG] Creating Tailscale directories..."
  TS_STATE_DIR="/tmp/tailscale"
  TS_RUN_DIR="/tmp/tailscale-run"
  TS_LIB_DIR="/tmp/tailscale-lib"
  
  mkdir -p "$TS_STATE_DIR" && echo "[DEBUG] Created $TS_STATE_DIR" || echo "[ERROR] Failed to create $TS_STATE_DIR"
  mkdir -p "$TS_RUN_DIR" && echo "[DEBUG] Created $TS_RUN_DIR" || echo "[ERROR] Failed to create $TS_RUN_DIR"
  mkdir -p "$TS_LIB_DIR" && echo "[DEBUG] Created $TS_LIB_DIR" || echo "[ERROR] Failed to create $TS_LIB_DIR"
  
  # Set environment variables for Tailscale to use our directories
  export TS_STATE_DIR="$TS_STATE_DIR"
  export TS_SOCKET_DIR="$TS_RUN_DIR"
  
  echo "[DEBUG] Starting tailscaled daemon..."
  echo "[DEBUG] State dir: $TS_STATE_DIR"
  echo "[DEBUG] Socket dir: $TS_RUN_DIR"
  
  # Start tailscaled in userspace networking mode (container-friendly)
  tailscaled --state="$TS_STATE_DIR/tailscaled.state" \
             --socket="$TS_RUN_DIR/tailscaled.sock" \
             --tun=userspace-networking \
             > /tmp/tailscaled.log 2>&1 &
  
  TS_PID=$!
  echo "[DEBUG] tailscaled started with PID: $TS_PID"
  
  # Wait briefly for tailscaled to come up
  echo "[DEBUG] Waiting for tailscaled to initialize..."
  sleep 3
  
  # Check if tailscaled is still running
  if ! kill -0 $TS_PID 2>/dev/null; then
    echo "[ERROR] tailscaled process died! Check logs:"
    cat /tmp/tailscaled.log || true
    echo "[WARN] Continuing without Tailscale..."
  else
    echo "[DEBUG] tailscaled is running, authenticating..."
    
    # Authenticate with Tailscale using only the auth key (outbound use-case)
    tailscale up --authkey="$TS_AUTHKEY" 2>&1 | while IFS= read -r line; do
      echo "[TAILSCALE] $line"
    done
    
    echo "[DEBUG] Waiting for Tailscale to connect..."
    RETRY_COUNT=0
    MAX_RETRIES=30
    while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
      if tailscale status > /dev/null 2>&1; then
        echo "[DEBUG] Tailscale connected successfully!"
        echo "[DEBUG] Tailscale status:"
        tailscale status
        break
      fi
      RETRY_COUNT=$((RETRY_COUNT + 1))
      echo "[DEBUG] Waiting for connection... (attempt $RETRY_COUNT/$MAX_RETRIES)"
      sleep 1
    done
    
    if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
      echo "[WARN] Tailscale did not connect within timeout, continuing anyway..."
      echo "[DEBUG] tailscaled logs:"
      cat /tmp/tailscaled.log || true
    fi
  fi
else
  echo "[DEBUG] TS_AUTHKEY is NOT SET - skipping Tailscale setup"
fi

echo "=========================================="
echo "[DEBUG] Starting .NET WebApi application..."
echo "[DEBUG] Command: dotnet WebApi.dll --urls http://+:8080"
echo "=========================================="

# Start the .NET application
exec dotnet WebApi.dll --urls "http://+:8080"


