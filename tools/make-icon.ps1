# Regenerates Assets\FastSnip.ico (16–256 px, PNG-compressed entries).
# Run from anywhere: powershell -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$out = Join-Path $PSScriptRoot '..\Assets\FastSnip.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function Draw-Icon([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    # Rounded blue tile
    $r = [Math]::Max(3, $s * 0.22)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r, $r, 180, 90); $path.AddArc($s - $r - 1, 0, $r, $r, 270, 90)
    $path.AddArc($s - $r - 1, $s - $r - 1, $r, $r, 0, 90); $path.AddArc(0, $s - $r - 1, $r, $r, 90, 90)
    $path.CloseFigure()
    $rect = New-Object Drawing.Rectangle 0, 0, $s, $s
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(0, 170, 255)), ([Drawing.Color]::FromArgb(0, 110, 210)), 90
    $g.FillPath($fill, $path)

    # White selection-corner brackets
    $w = [Math]::Max(1.5, $s * 0.09)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::White), $w
    $pen.StartCap = 'Square'; $pen.EndCap = 'Square'
    $m = $s * 0.24; $l = $s * 0.2; $e = $s - $m
    $g.DrawLines($pen, [Drawing.PointF[]]@((New-Object Drawing.PointF $m, ($m + $l)), (New-Object Drawing.PointF $m, $m), (New-Object Drawing.PointF ($m + $l), $m)))
    $g.DrawLines($pen, [Drawing.PointF[]]@((New-Object Drawing.PointF ($e - $l), $m), (New-Object Drawing.PointF $e, $m), (New-Object Drawing.PointF $e, ($m + $l))))
    $g.DrawLines($pen, [Drawing.PointF[]]@((New-Object Drawing.PointF $m, ($e - $l)), (New-Object Drawing.PointF $m, $e), (New-Object Drawing.PointF ($m + $l), $e)))

    # Red "pixel" in the bottom-right corner: the pixel-exact pencil
    $g.SmoothingMode = 'None'
    $p = [Math]::Max(3, [int]($s * 0.2))
    $red = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 70, 70))
    $g.FillRectangle($red, [int]($e - $p + $w / 2), [int]($e - $p + $w / 2), $p, $p)

    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$images = foreach ($s in $sizes) { , (Draw-Icon $s) }

$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256))   # 256 is stored as 0
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
"Wrote $out"
