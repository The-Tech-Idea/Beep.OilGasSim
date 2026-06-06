Write-Host "Running persistence setup wizard..."
Set-Location $PSScriptRoot
dotnet run --project src/Beep.OilGasSim.Api -- setup
