# Generates JungleGame.UI/Assets/jungle.ico (multi-size, PNG-compressed):
# a blue disc with a stylized gold lion face. Re-run after tweaking the drawing
# code below; the .ico is committed so builds do not depend on this script.
Add-Type -AssemblyName System.Drawing

function New-LionBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $s = [double]$size
    $padValue = [double]($s * 0.04)
    if ($padValue -lt 1.0) { $padValue = 1.0 }
    $discWidth = [double]($s - 2.0 * $padValue)

    $disc = New-Object System.Drawing.RectangleF -ArgumentList $padValue, $padValue, $discWidth, $discWidth

    # Blue disc background
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $disc,
        [System.Drawing.Color]::FromArgb(255, 110, 142, 240),
        [System.Drawing.Color]::FromArgb(255, 33, 73, 177),
        90)
    $g.FillEllipse($bgBrush, $disc)

    $gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 215, 0))
    $faceCol = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 222, 120))
    $dark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 60, 40, 20))

    # Ears
    $earR = [double]($s * 0.10)
    $g.FillEllipse($gold, [float]($s * 0.26 - $earR), [float]($s * 0.20 - $earR), [float](2 * $earR), [float](2 * $earR))
    $g.FillEllipse($gold, [float]($s * 0.74 - $earR), [float]($s * 0.20 - $earR), [float](2 * $earR), [float](2 * $earR))

    # Mane
    $maneR = [double]($s * 0.40)
    $g.FillEllipse($gold, [float]($s * 0.50 - $maneR), [float]($s * 0.52 - $maneR), [float](2 * $maneR), [float](2 * $maneR))

    # Face
    $faceR = [double]($s * 0.24)
    $g.FillEllipse($faceCol, [float]($s * 0.50 - $faceR), [float]($s * 0.54 - $faceR), [float](2 * $faceR), [float](2 * $faceR))

    # Eyes
    $eyeR = [double]($s * 0.035)
    $g.FillEllipse($dark, [float]($s * 0.41 - $eyeR), [float]($s * 0.50 - $eyeR), [float](2 * $eyeR), [float](2 * $eyeR))
    $g.FillEllipse($dark, [float]($s * 0.59 - $eyeR), [float]($s * 0.50 - $eyeR), [float](2 * $eyeR), [float](2 * $eyeR))

    # Nose
    $noseR = [double]($s * 0.05)
    $g.FillEllipse($dark, [float]($s * 0.50 - $noseR), [float]($s * 0.61 - $noseR), [float](2 * $noseR), [float](2 * $noseR))

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bmp = New-LionBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs.Add($ms.ToArray())
    $ms.Dispose()
}

# Assemble the ICO container (PNG-compressed entries)
$outDir = Join-Path $PSScriptRoot "..\JungleGame.UI\Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type: icon
$bw.Write([uint16]$pngs.Count)    # entry count
$offset = 6 + 16 * $pngs.Count
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $size = $sizes[$i]
    $bw.Write([byte]($size % 256))  # width (256 encodes as 0)
    $bw.Write([byte]($size % 256))  # height
    $bw.Write([byte]0)              # color count
    $bw.Write([byte]0)              # reserved
    $bw.Write([uint16]1)            # planes
    $bw.Write([uint16]32)           # bits per pixel
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()
$icoPath = Join-Path $outDir "jungle.ico"
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes)"
