## Testing

### Frontend unit + component tests (fast)
```bash
dotnet test tests/WebFrontend.Tests
```

### Frontend E2E tests (Playwright, not run by default)
1) Install Playwright browsers (one-time per machine):

```bash
dotnet build tests/WebFrontend.Tests.E2E
pwsh tests/WebFrontend.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install
```

2) Run E2E (will start/stop docker-compose):

```bash
dotnet test tests/WebFrontend.Tests.E2E --filter Category=E2E
```


