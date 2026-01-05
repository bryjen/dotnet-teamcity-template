$startTime = Get-Date

if (-not $env:ConnectionStrings__DefaultConnection) {
  Write-Error "Missing env var: ConnectionStrings__DefaultConnection. Set it (or run via docker-compose where it's provided) before running this script."
  exit 1
}

cd "..\WebApi"

echo "[Database:Drop] WebApi.Data.AppDbContext"
dotnet ef database drop -f --context WebApi.Data.AppDbContext

$date = Get-Date -Format "yyyyMMddHHmm"
echo "[Migrations:Add] WebApi.Data.AppDbContext"
dotnet ef migrations add "${date}_TodoApp" --context WebApi.Data.AppDbContext --verbose

echo "[Database:Update] WebApi.Data.AppDbContext"
dotnet ef database update --context WebApi.Data.AppDbContext --verbose
Start-Sleep -Seconds 2

# seeding the db
cd "..\WebApi.Seeding"
echo "[INFO] Seeding the db"
dotnet run

$endTime = Get-Date
$duration = $endTime - $startTime
Write-Host "[SUCCESS] Script completed in $($duration.TotalSeconds.ToString('F2')) seconds"
