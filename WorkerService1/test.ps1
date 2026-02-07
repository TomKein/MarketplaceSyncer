# Quick test runner for BusinessRu API
# Usage: .\test.ps1

Write-Host "Starting API tests..." -ForegroundColor Cyan
Write-Host ""

# Stop any running instances
Get-Process -Name "WorkerService1" -ErrorAction SilentlyContinue | Stop-Process -Force

# Run tests
dotnet run --project WorkerService1.csproj -- --test

Write-Host ""
Write-Host "Tests completed. Press any key to exit..." -ForegroundColor Green
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
