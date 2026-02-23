$appDir = "bin\Debug\net10.0-windows\"

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
    Write-Host "Copy to: $(Resolve-Path $appDir)" -ForegroundColor Yellow
}
