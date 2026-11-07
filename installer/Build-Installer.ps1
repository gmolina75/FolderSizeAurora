# ============================================================================
#  Folder Size Aurora - compila el instalador MSI
# ============================================================================
#  Hace todo el proceso:
#    1. Publica la app autocontenida (single file) en dist-standalone\
#    2. Genera el icono .ico si falta
#    3. Compila las DOS variantes del MSI desde el mismo .wxs editable:
#         FolderSizeAurora.msi       -> por maquina (Program Files, requiere admin)
#         FolderSizeAurora-user.msi  -> por usuario (AppData, sin admin)
#
#  Uso:
#    powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1
#    powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1 -Version 1.1.0
#    powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1 -UserOnly
# ============================================================================

[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [string] $Configuration = 'Release',
    [switch] $UserOnly,
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $root 'foldersizeaurora\FolderSizeAurora.csproj'
$wxs        = Join-Path $root 'installer\FolderSizeAurora.wxs'
$publishDir = Join-Path $root 'dist-standalone'
$outDir     = Join-Path $root 'installer'

# GUID fijo de la familia de producto. NO lo cambies entre versiones:
# es lo que permite que una release actualice la anterior.
$upgradeCode = '{C1A4341F-2C89-4BC2-A3A7-19C4ED0C5A8C}'

Write-Host ''
Write-Host '=== Folder Size Aurora - constructor del instalador ===' -ForegroundColor Cyan
Write-Host "Version : $Version"
Write-Host "Raiz    : $root"
Write-Host ''

if (-not $SkipPublish) {
    Write-Host '=== 1/3 Publicando la aplicacion (autocontenida) ===' -ForegroundColor Cyan

    & dotnet publish $project `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) { throw "Fallo el publish (codigo $LASTEXITCODE)" }

    $exe = Join-Path $publishDir 'FolderSizeAurora.exe'
    if (-not (Test-Path $exe)) { throw "No se genero el ejecutable en $publishDir" }
    Write-Host ("Ejecutable: {0} ({1} MB)" -f $exe, [math]::Round((Get-Item $exe).Length / 1MB, 1)) -ForegroundColor Green
} else {
    Write-Host '=== 1/3 Publish omitido (SkipPublish) ===' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host '=== 2/3 Generando el icono ===' -ForegroundColor Cyan

$ico = Join-Path $outDir 'FolderSizeAurora.ico'
if (-not (Test-Path $ico)) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $root 'tools\Make-Ico.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la generacion del icono' }
} else {
    Write-Host "Icono ya existe: $ico" -ForegroundColor DarkGray
}

Write-Host ''
Write-Host '=== 3/3 Compilando los MSI ===' -ForegroundColor Cyan

function Build-Msi {
    param(
        [string] $OutName,
        [string] $Scope,
        [string] $RootDir,
        [string] $InstallFolderId,
        [string] $RegistryRoot
    )

    $outPath = Join-Path $outDir $OutName
    Write-Host ("  -> {0}  ({1})" -f $OutName, $Scope) -ForegroundColor White

    Push-Location $outDir
    try {
        & wix build FolderSizeAurora.wxs -o $OutName `
            -d "Version=$Version" `
            -d "UpgradeCode=$upgradeCode" `
            -d "Scope=$Scope" `
            -d "RootDir=$RootDir" `
            -d "InstallFolderId=$InstallFolderId" `
            -d "RegistryRoot=$RegistryRoot"
        if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de $OutName (codigo $LASTEXITCODE)" }
    } finally {
        Pop-Location
    }

    Write-Host ("     {0} MB" -f [math]::Round((Get-Item $outPath).Length / 1MB, 2)) -ForegroundColor DarkGray
    return $outPath
}

$built = @()

if (-not $UserOnly) {
    $built += Build-Msi -OutName 'FolderSizeAurora.msi' -Scope 'perMachine' `
                          -RootDir 'ProgramFiles64Folder' -InstallFolderId 'INSTALLFOLDER' `
                          -RegistryRoot 'HKLM'
}

$built += Build-Msi -OutName 'FolderSizeAurora-user.msi' -Scope 'perUser' `
                          -RootDir 'LocalAppDataFolder' -InstallFolderId 'APPDATA_FOLDER' `
                          -RegistryRoot 'HKCU'

# Copia la variante principal a la carpeta de la landing, si existe
$landingDir = Join-Path $root 'landing'
if (Test-Path $landingDir) {
    $main = Join-Path $outDir 'FolderSizeAurora.msi'
    if (Test-Path $main) {
        Copy-Item $main (Join-Path $landingDir 'FolderSizeAurora.msi') -Force
        Write-Host "Copia en landing\FolderSizeAurora.msi" -ForegroundColor DarkGray
    }
}

Write-Host ''
Write-Host 'Instaladores listos:' -ForegroundColor Green
foreach ($b in $built) {
    Write-Host ("  {0}  ({1} MB)" -f $b, [math]::Round((Get-Item $b).Length / 1MB, 2))
}
Write-Host ''
Write-Host 'Instalar por usuario (sin Administrador):' -ForegroundColor Gray
Write-Host '  msiexec /i FolderSizeAurora-user.msi'
Write-Host 'Instalar por maquina (requiere Administrador):' -ForegroundColor Gray
Write-Host '  msiexec /i FolderSizeAurora.msi'
Write-Host 'Instalar en silencio:' -ForegroundColor Gray
Write-Host '  msiexec /i FolderSizeAurora-user.msi /qn'
Write-Host ''
