$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Join-Path $scriptDir "bin\Debug\net10.0-windows\"
$projectPath = Join-Path $scriptDir "RandySDR.csproj"

Write-Host "Building app and runtime dependencies..." -ForegroundColor Yellow
& dotnet build $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. The app was not started." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Checking for required DLLs..." -ForegroundColor Yellow

$rtlsdr = Test-Path (Join-Path $appDir "rtlsdr.dll")
$libusb = Test-Path (Join-Path $appDir "libusb-1.0.dll")

Write-Host "rtlsdr.dll: " -NoNewline
if ($rtlsdr) { Write-Host "FOUND" -ForegroundColor Green } else { Write-Host "MISSING" -ForegroundColor Red }

Write-Host "libusb-1.0.dll: " -NoNewline
if ($libusb) { Write-Host "FOUND" -ForegroundColor Green } else { Write-Host "MISSING" -ForegroundColor Red }

if ($rtlsdr -and $libusb) {
    Write-Host "`nAll DLLs found! Starting app..." -ForegroundColor Green
    Start-Process (Join-Path $appDir "RandySDR.exe")
} else {
    Write-Host "`nMissing DLLs! Please copy them from your SDR# installation." -ForegroundColor Red
    if (Test-Path $appDir) {
        Write-Host "Copy to: $(Resolve-Path $appDir)" -ForegroundColor Yellow
    } else {
        Write-Host "Build the project first. Run: dotnet build" -ForegroundColor Yellow
    }
}
