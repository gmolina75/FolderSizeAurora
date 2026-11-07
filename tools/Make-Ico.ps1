# Convierte landing\assets\logo-256.png en un .ico multirresolucion (16/24/32/48/64/128/256).
# WiX necesita un .ico real para el icono del producto y de los accesos directos.
# Uso:  powershell -ExecutionPolicy Bypass -File tools\Make-Ico.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent $PSScriptRoot
$srcPng = Join-Path $root 'landing\assets\logo-256.png'
$outIco = Join-Path $root 'installer\FolderSizeAurora.ico'

if (-not (Test-Path $srcPng)) { throw "No se encuentra el PNG origen: $srcPng" }

$dir = Split-Path -Parent $outIco
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$src   = [System.Drawing.Image]::FromFile($srcPng)

# ICO con entradas PNG (formato admitido por Windows Vista+ y por WiX)
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR
$bw.Write([UInt16]0)            # reservado
$bw.Write([UInt16]1)            # tipo: icono
$bw.Write([UInt16]$sizes.Count) # numero de entradas

$entries = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()

    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $entries += ,@{ Size = $s; Data = $pngMs.ToArray() }
    $pngMs.Dispose()
}
$src.Dispose()

# ICONDIRENTRY + datos
$offset = 6 + (16 * $sizes.Count)
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $bw.Write([Byte]$dim)                                  # ancho
    $bw.Write([Byte]$dim)                                  # alto
    $bw.Write([Byte]0)                                     # colores de paleta
    $bw.Write([Byte]0)                                     # reservado
    $bw.Write([UInt16]1)                                   # planos
    $bw.Write([UInt16]32)                                  # bits por pixel
    $bw.Write([UInt32]$e.Data.Length)                      # bytes de la imagen
    $bw.Write([UInt32]$offset)                             # desplazamiento
    $offset += $e.Data.Length
}
foreach ($e in $entries) { $bw.Write($e.Data) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($outIco, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()

Write-Host "ICO generado: $outIco ($([math]::Round((Get-Item $outIco).Length / 1KB, 1)) KB)"
