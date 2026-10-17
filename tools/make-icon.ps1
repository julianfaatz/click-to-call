# Generates src/ClickToCallForSnom/Assets/app.ico (blue rounded square with a white handset).
# Requires Windows 11 (Segoe Fluent Icons) or Windows 10 (Segoe MDL2 Assets).
param([string]$OutFile = "$PSScriptRoot\..\src\ClickToCallForSnom\Assets\app.ico")

Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$fontName = if ((New-Object System.Drawing.Text.InstalledFontCollection).Families.Name -contains "Segoe Fluent Icons") { "Segoe Fluent Icons" } else { "Segoe MDL2 Assets" }
$glyph = [string][char]0xE717

$pngs = foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"
    $g.TextRenderingHint = "AntiAliasGridFit"
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [Math]::Max(3, $size * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $size - 1
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $size, $size), ([System.Drawing.Color]::FromArgb(255, 37, 125, 235)), ([System.Drawing.Color]::FromArgb(255, 17, 75, 170))
    $g.FillPath($brush, $path)

    $font = New-Object System.Drawing.Font $fontName, ($size * 0.56), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = "Center"
    $format.LineAlignment = "Center"
    $rect = New-Object System.Drawing.RectangleF 0, ($size * 0.02), $size, $size
    $g.DrawString($glyph, $font, [System.Drawing.Brushes]::White, $rect, $format)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($size, $ms.ToArray())
}

# ICO container with PNG compressed entries
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $s = $p[0]; $data = $p[1]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($p in $pngs) { $bw.Write([byte[]]$p[1]) }
$bw.Flush()

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
[System.IO.File]::WriteAllBytes($OutFile, $out.ToArray())
Write-Host "Wrote $OutFile"
