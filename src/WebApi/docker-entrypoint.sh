#!/bin/sh
set +e  # Don't exit on error, we want to see what's happening

echo "=========================================="
echo "WebApi Container Startup"
echo "=========================================="

# Always log environment check for debugging
echo "[DEBUG] Checking for TS_AUTHKEY environment variable..."
if [ -n "$TS_AUTHKEY" ]; then
  echo "[DEBUG] TS_AUTHKEY is SET (length: ${#TS_AUTHKEY} chars)"
  echo "[DEBUG] TS_AUTHKEY prefix: ${TS_AUTHKEY%%-*}-****"
else
  echo "[DEBUG] TS_AUTHKEY is NOT SET or is empty"
  echo "[DEBUG] This means Tailscale will be skipped"
fi

# Check if Tailscale auth key is provided
if [ -n "$TS_AUTHKEY" ]; then
  echo "[DEBUG] TS_AUTHKEY is SET (length: ${#TS_AUTHKEY} chars)"
  echo "[DEBUG] TS_AUTHKEY prefix: ${TS_AUTHKEY%%-*}-****"
  echo "[DEBUG] Starting Tailscale setup..."
  
  # Check if tailscaled exists
  if ! command -v tailscaled >/dev/null 2>&1; then
    echo "[ERROR] tailscaled command not found! Is Tailscale installed?"
    echo "[ERROR] Continuing without Tailscale"
  else
    echo "[DEBUG] tailscaled found at: $(which tailscaled)"
    
    # Start tailscaled in userspace networking mode, using in-memory state
    # This avoids any filesystem permission issues in Cloud Run.
    echo "[DEBUG] Starting tailscaled daemon (state=mem)..."
    tailscaled --tun=userspace-networking --state=mem: > /tmp/tailscaled.log 2>&1 &
    
    TS_PID=$!
    echo "[DEBUG] tailscaled started with PID: $TS_PID"
    echo "[DEBUG] Checking if PID $TS_PID is valid..."
    
    # Wait for tailscaled to initialize
    echo "[DEBUG] Waiting for tailscaled to initialize (3 seconds)..."
    sleep 3
    
    # Check if tailscaled is still running
    echo "[DEBUG] Checking if tailscaled process is still alive..."
    if ! kill -0 $TS_PID 2>/dev/null; then
      echo "[ERROR] tailscaled process died! Check logs:"
      cat /tmp/tailscaled.log || echo "[ERROR] Could not read tailscaled.log"
      echo "[ERROR] Tailscale daemon failed to start - continuing without Tailscale"
    else
      echo "[DEBUG] tailscaled is still running (PID: $TS_PID)"
      echo "[DEBUG] tailscaled is running, authenticating with Tailscale..."
      
      # Authenticate with Tailscale using the auth key
      AUTH_OUTPUT=$(tailscale up --authkey="$TS_AUTHKEY" 2>&1)
      AUTH_EXIT=$?
      
      if [ $AUTH_EXIT -ne 0 ]; then
        echo "[ERROR] Tailscale authentication failed!"
        echo "[ERROR] tailscale up output: $AUTH_OUTPUT"
        echo "[ERROR] Continuing without Tailscale connectivity"
      else
        echo "[TAILSCALE] Authentication successful"
        echo "[TAILSCALE] $AUTH_OUTPUT"
        
        # Wait a bit for connection to establish
        echo "[DEBUG] Waiting for Tailscale connection to establish..."
        sleep 5
        
        # Verify Tailscale connection
        echo "[DEBUG] Verifying Tailscale connection..."
        if tailscale status > /dev/null 2>&1; then
          echo "[SUCCESS] Tailscale is connected and working!"
          echo "[DEBUG] Tailscale status:"
          tailscale status
          echo "[DEBUG] Tailscale IP addresses:"
          tailscale ip -4 || echo "[WARN] Could not get Tailscale IP"
          
          # Test connectivity to database server if connection string is available
          # Check both environment variable formats
          DB_CONN_STR="${ConnectionStrings__DefaultConnection:-${CONNECTIONSTRINGS__DEFAULTCONNECTION:-}}"
          if [ -n "$DB_CONN_STR" ]; then
            # Extract server IP from connection string (format: Server=IP,PORT or Server=IP:PORT)
            DB_SERVER=$(echo "$DB_CONN_STR" | sed -n 's/.*Server=\([^,;]*\).*/\1/p' | head -1)
            if [ -n "$DB_SERVER" ]; then
              # Extract just the IP (remove port if present)
              DB_IP=$(echo "$DB_SERVER" | cut -d',' -f1 | cut -d':' -f1)
              echo "[DEBUG] Testing Tailscale connectivity to database IP: $DB_IP"
              if tailscale ping -c 1 -timeout 5s "$DB_IP" > /dev/null 2>&1; then
                echo "[SUCCESS] Can reach database server at $DB_IP via Tailscale"
              else
                echo "[WARN] Cannot reach database server at $DB_IP via Tailscale ping"
                echo "[WARN] This may indicate routing issues, but SQL connection may still work"
              fi
            fi
          fi
        else
          echo "[ERROR] Tailscale connection verification failed!"
          echo "[ERROR] tailscale status command failed"
          echo "[ERROR] Check tailscaled logs:"
          tail -n 20 /tmp/tailscaled.log || true
          echo "[ERROR] Continuing without Tailscale connectivity"
        fi
      fi
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


