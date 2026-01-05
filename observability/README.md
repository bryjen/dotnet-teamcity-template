## Observability (Aspire Dashboard + OpenTelemetry Collector)

This folder contains the OpenTelemetry Collector configuration used by the solution-wide compose setup.

### What you get
- **OpenTelemetry Collector**: receives OTLP traces from `webapi`
- **Aspire Dashboard**: view traces in a local UI

### Run
From the solution root:

```bash
docker compose up --build
```

### Access
- **Aspire Dashboard UI**: `http://localhost:18888` (bound to `127.0.0.1`)

### Notes
- `webapi` is configured (in root `docker-compose.yml`) to export traces to `otel-collector` via:
  - `OTEL_SERVICE_NAME=WebApi`
  - `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317`


