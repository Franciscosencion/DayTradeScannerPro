<#
.SYNOPSIS
    Builds a self-contained MSIX package for Day Trade Scanner Pro.

.DESCRIPTION
    Steps:
      1. Publishes TradeScanner.UI as self-contained x64 (dotnet publish).
      2. Assembles the MSIX layout directory (app files + manifest + assets).
      3. Runs MakeAppx.exe (Windows 10/11 SDK) to produce the .msix file.
      4. Optionally creates a self-signed test cert and signs for sideloading.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Version
    Package version Major.Minor.Build.Revision. Default: 1.0.0.0.

.PARAMETER Sign
    Create a self-signed certificate and sign the MSIX for local sideloading.

.PARAMETER CertPassword
    Password for the self-signed PFX. Default: TradeScanner2026!

.PARAMETER WindowsSdkBin
    Explicit path to Windows SDK bin x64 folder containing MakeAppx.exe.
    Auto-detected from "C:\Program Files (x86)\Windows Kits\10\bin" if omitted.

.EXAMPLE
    .\Build-Msix.ps1
    .\Build-Msix.ps1 -Sign
    .\Build-Msix.ps1 -Version 1.2.0.0 -Sign
#>
param(
    [string] $Configuration  = "Release",
    [string] $Version        = "1.0.0.0",
    [switch] $Sign,
    [string] $CertPassword   = "TradeScanner2026!",
    [string] $WindowsSdkBin  = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- Paths -------------------------------------------------------------------
$Root        = $PSScriptRoot
$DotnetExe   = "C:\Program Files (x86)\dotnet\dotnet.exe"
$UiProject   = Join-Path $Root "TradeScanner.UI\TradeScanner.UI.csproj"
$PublishDir  = Join-Path $Root "publish\win-x64"
$LayoutDir   = Join-Path $Root "publish\msix-layout"
$OutputDir   = Join-Path $Root "publish\output"
$MsixPath    = Join-Path $OutputDir "TradeScanner_$Version.msix"
$ManifestSrc = Join-Path $Root "TradeScanner.UI\Package.appxmanifest"
$AssetsDir   = Join-Path $Root "TradeScanner.UI\Assets"

# --- Helpers -----------------------------------------------------------------
function Step { param([string]$Msg) Write-Host "" ; Write-Host "==> $Msg" -ForegroundColor Cyan }
function Ok   { param([string]$Msg) Write-Host "    OK: $Msg" -ForegroundColor Green }
function Warn { param([string]$Msg) Write-Host "    WARN: $Msg" -ForegroundColor Yellow }

function Find-WindowsSdkBin {
    $base = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $base)) { return $null }
    $dir = Get-ChildItem $base -Directory |
        Where-Object { $_.Name -match "^\d+\.\d+\.\d+\.\d+$" } |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1
    if ($dir) { return Join-Path $dir.FullName "x64" }
    return $null
}

# --- 1. Locate Windows SDK ---------------------------------------------------
Step "Locating Windows SDK tools"

if (-not $WindowsSdkBin) { $WindowsSdkBin = Find-WindowsSdkBin }

$MakeAppx = $null
$SignTool  = $null
if ($WindowsSdkBin) {
    $candidateMake = Join-Path $WindowsSdkBin "makeappx.exe"
    $candidateSign = Join-Path $WindowsSdkBin "signtool.exe"
    if (Test-Path $candidateMake) { $MakeAppx = $candidateMake }
    if (Test-Path $candidateSign) { $SignTool  = $candidateSign }
}

if ($MakeAppx) {
    Ok "MakeAppx.exe: $MakeAppx"
} else {
    Warn "MakeAppx.exe not found. Install Windows 10/11 SDK to create the .msix file."
    Warn "Layout will be produced at: $LayoutDir"
}

# --- 2. Patch version in manifest --------------------------------------------
Step "Patching manifest version to $Version"

$manifestContent = Get-Content $ManifestSrc -Raw
# -creplace is case-sensitive: preserves lowercase 'version' in <?xml> declaration
$patched = $manifestContent -creplace 'Version="[\d\.]+"', ('Version="' + $Version + '"')
$tmpManifest = Join-Path $env:TEMP "Package.appxmanifest"
# Write without BOM — PS 5.1 Set-Content -Encoding UTF8 adds a BOM that MakeAppx rejects
[System.IO.File]::WriteAllText($tmpManifest, $patched, [System.Text.UTF8Encoding]::new($false))
Ok "Manifest staged at: $tmpManifest"

# --- 3. Clean output dirs (must happen before PATH is mutated) ---------------
Step "Cleaning output directories"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $LayoutDir)  { Remove-Item $LayoutDir  -Recurse -Force }
New-Item -ItemType Directory $PublishDir | Out-Null
New-Item -ItemType Directory $LayoutDir  | Out-Null
New-Item -ItemType Directory $OutputDir  -Force | Out-Null
Ok "Output dirs ready"

# --- 4. Publish the app ------------------------------------------------------
Step "Publishing TradeScanner.UI ($Configuration, win-x64, self-contained)"

# Set x86 SDK on PATH (must come after Remove-Item calls — sandbox blocks deletion after PATH change)
$env:PATH = "C:\Program Files (x86)\dotnet;" + $env:PATH
$env:DOTNET_ROOT = "C:\Program Files (x86)\dotnet"
$env:DOTNET_MULTILEVEL_LOOKUP = "0"

& $DotnetExe publish $UiProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=false `
    -o $PublishDir `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
Ok "Published to $PublishDir"

# --- 5. Assemble MSIX layout -------------------------------------------------
Step "Assembling MSIX layout"

Copy-Item "$PublishDir\*" $LayoutDir -Recurse -Force
Ok "Copied app binaries"

Copy-Item $tmpManifest (Join-Path $LayoutDir "AppxManifest.xml") -Force
Ok "Copied AppxManifest.xml"

$layoutAssets = Join-Path $LayoutDir "Assets"
if (-not (Test-Path $layoutAssets)) { New-Item -ItemType Directory $layoutAssets | Out-Null }
Copy-Item "$AssetsDir\*" $layoutAssets -Force
Ok "Copied Assets"

# --- 6. Pack MSIX ------------------------------------------------------------
if ($MakeAppx) {
    Step "Running MakeAppx.exe"
    if (Test-Path $MsixPath) { Remove-Item $MsixPath -Force }
    & $MakeAppx pack /d $LayoutDir /p $MsixPath /nv /o
    if ($LASTEXITCODE -ne 0) { throw "MakeAppx.exe failed (exit $LASTEXITCODE)" }
    Ok "MSIX created: $MsixPath"
} else {
    Warn "MakeAppx not available. To pack manually:"
    Warn "  makeappx.exe pack /d `"$LayoutDir`" /p `"$MsixPath`" /nv"
}

# --- 7. Sign (optional) ------------------------------------------------------
if ($Sign) {
    if (-not $MakeAppx) { Warn "Skipping signing (MSIX was not produced)." }
    elseif (-not (Test-Path $MsixPath)) { Warn "Skipping signing (MSIX file missing)." }
    else {
        Step "Creating self-signed test certificate"
        $certSubject = "CN=TradeScanner"
        $pfxPath     = Join-Path $OutputDir "TradeScanner-test.pfx"
        $secPwd      = ConvertTo-SecureString $CertPassword -AsPlainText -Force

        $cert = New-SelfSignedCertificate `
            -Subject $certSubject `
            -Type CodeSigningCert `
            -KeyUsage DigitalSignature `
            -FriendlyName "TradeScanner Test Signing" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(3)

        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secPwd | Out-Null
        Ok "Certificate exported: $pfxPath"

        if ($SignTool) {
            Step "Signing MSIX"
            & $SignTool sign /fd SHA256 /a /f $pfxPath /p $CertPassword $MsixPath
            if ($LASTEXITCODE -ne 0) { throw "SignTool.exe failed (exit $LASTEXITCODE)" }
            Ok "Package signed."

            $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
                "TrustedPeople", "LocalMachine")
            $store.Open("ReadWrite")
            $store.Add($cert)
            $store.Close()
            Ok "Certificate installed to LocalMachine\TrustedPeople for sideloading."
        } else {
            Warn "SignTool.exe not found. Sign manually:"
            Warn "  signtool.exe sign /fd SHA256 /f `"$pfxPath`" /p $CertPassword `"$MsixPath`""
        }
    }
}

# --- Summary -----------------------------------------------------------------
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host "  Layout : $LayoutDir"

if (Test-Path $MsixPath) {
    $sizeMb = (Get-Item $MsixPath).Length / 1MB
    Write-Host ("  MSIX   : $MsixPath  ({0:F1} MB)" -f $sizeMb) -ForegroundColor Green
    if ($Sign) {
        Write-Host "  Install: Add-AppxPackage -Path `"$MsixPath`""
    } else {
        Write-Host "  Note   : Package is unsigned. Run with -Sign to enable sideloading."
    }
} else {
    Write-Host "  MSIX   : Not produced (install Windows 10/11 SDK for MakeAppx.exe)." -ForegroundColor Yellow
}
