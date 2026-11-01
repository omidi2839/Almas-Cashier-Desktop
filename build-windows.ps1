$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'Microsoft .NET 8 SDK is required on the BUILD computer.' -ForegroundColor Yellow
    Write-Host 'Install from https://dotnet.microsoft.com/download/dotnet/8.0 then run this file again.'
    exit 2
}
New-Item -ItemType Directory -Force -Path (Join-Path $root 'artifacts\publish') | Out-Null
& dotnet publish (Join-Path $root 'AlmasCashierDesktop\AlmasCashierDesktop.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root 'artifacts\publish')
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
$exe = Join-Path $root 'artifacts\publish\AlmasCashierDesktop.exe'
if (-not (Test-Path $exe)) { throw 'EXE missing after build' }
Write-Host ('WINDOWS EXE: ' + $exe) -ForegroundColor Green
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if ($iscc) {
    & $iscc (Join-Path $root 'installer\AlmasCashier.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Setup builder failed.' }
    Write-Host ('WINDOWS INSTALLER: ' + (Join-Path $root 'artifacts\installer\AlmasCashierDesktop_v0.1.0_Setup_Windows_x64.exe')) -ForegroundColor Green
} else {
    Write-Host 'Inno Setup 6 not installed. A self-contained EXE was built; install Inno Setup 6 for standard Setup.exe.' -ForegroundColor Yellow
}
