# Script to copy RTL-SDR DLLs from SDR# to RandySDR

$appDir = "bin\Debug\net10.0-windows\"

Write-Host "Searching for SDR# installation..." -ForegroundColor Yellow

# Common SDR# locations
$searchPaths = @(
    "C:\Program Files\SDRSharp",
    "C:\Program Files (x86)\SDRSharp",
    "$env:USERPROFILE\Downloads\SDRSharp",
    "$env:USERPROFILE\Desktop\SDRSharp",
    "C:\SDRSharp",
    "D:\SDRSharp"
)

# Add any folder in Downloads that contains SDRSharp
$downloadsPath = "$env:USERPROFILE\Downloads"
if (Test-Path $downloadsPath) {
    Get-ChildItem -Path $downloadsPath -Directory | ForEach-Object {
        if (Test-Path "$($_.FullName)\rtlsdr.dll") {
            $searchPaths += $_.FullName
        }
    }
}

$foundPath = $null
foreach ($path in $searchPaths) {
    Write-Host "Checking: $path" -ForegroundColor Gray
    if (Test-Path "$path\rtlsdr.dll") {
        $foundPath = $path
        Write-Host "Found SDR# at: $foundPath" -ForegroundColor Green
        break
    }
}

if ($foundPath) {
    $requiredDlls = @("rtlsdr.dll", "libusb-1.0.dll")
    
    Write-Host "`nCopying DLLs to $appDir..." -ForegroundColor Yellow
    
    foreach ($dll in $requiredDlls) {
        $source = Join-Path $foundPath $dll
        $dest = Join-Path $appDir $dll
        
        if (Test-Path $source) {
            Copy-Item -Path $source -Destination $dest -Force
            Write-Host "  Copied: $dll" -ForegroundColor Green
        } else {
            Write-Host "  Missing: $dll" -ForegroundColor Red
        }
    }
    
    Write-Host "`nDone! Run the app now." -ForegroundColor Green
} else {
    Write-Host "`nSDR# installation not found!" -ForegroundColor Red
    Write-Host "`nPlease manually copy these files to $appDir :" -ForegroundColor Yellow
    Write-Host "  - rtlsdr.dll"
    Write-Host "  - libusb-1.0.dll"
    Write-Host "`nYou can download SDR# from: https://airspy.com/download/" -ForegroundColor Cyan
    Write-Host "After extracting, find the files in the SDR# folder." -ForegroundColor Cyan
}
