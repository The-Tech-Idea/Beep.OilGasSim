# Start API + web client (Windows)

Write-Host "Starting Beep Oil and Gas Sim API on http://localhost:5080 ..."
$api = Start-Process -PassThru -FilePath "dotnet" -ArgumentList @(
    "run",
    "--project", "$PSScriptRoot\src\Beep.OilGasSim.Api"
) -WorkingDirectory $PSScriptRoot

Start-Sleep -Seconds 4

try {
    $health = Invoke-RestMethod -Uri "http://localhost:5080/health" -TimeoutSec 5
    Write-Host "API: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "API not responding yet. Check the API window for build errors." -ForegroundColor Yellow
}

Write-Host "Starting web client on http://localhost:5173 ..."
Push-Location "$PSScriptRoot\client\beep-oil-gas-sim-web"
& .\dev.cmd
Pop-Location

if ($api -and -not $api.HasExited) {
    Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
}
