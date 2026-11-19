# Run with Windows PowerShell in STA mode: powershell -NoProfile -STA -File scripts/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$sourcePath = Join-Path $root 'assets\icon-source.png'
$target = Join-Path $root 'src\ReferenceOverlayViewer\Assets'
New-Item -ItemType Directory -Force $target | Out-Null
$source = New-Object System.Windows.Media.Imaging.BitmapImage
$source.BeginInit()
$source.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$source.UriSource = [Uri]$sourcePath
$source.EndInit()
$source.Freeze()
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $visual.RenderOpen()
    $context.DrawImage($source, (New-Object System.Windows.Rect(0,0,$size,$size)))
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    $frames += ,$bytes
    if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $target 'app-icon.png'), $bytes) }
}
# ICO directory with a PNG-compressed RGBA frame per display size (Windows Vista and later).
$icoStream = [IO.File]::Create((Join-Path $target 'app.ico'))
$writer = New-Object IO.BinaryWriter($icoStream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = $sizes[$i] % 256
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $icoStream.Dispose() }
Write-Output ('Generated icon sizes: ' + ($sizes -join ', '))
