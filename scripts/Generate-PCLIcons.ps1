[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\Images\Branding'
$original = Join-Path $destination 'pcl-original.ico'
if (-not (Test-Path -LiteralPath $original)) { throw 'The pinned original PCL icon is missing.' }
Copy-Item -LiteralPath $original -Destination (Join-Path $destination 'p1-launcher.ico')
$originalBytes = [IO.File]::ReadAllBytes($original)
function Get-OriginalIconBitmap([int]$Size) {
    $entries = for ($index=0; $index -lt [BitConverter]::ToUInt16($originalBytes,4); $index++) {
        $entryOffset = 6 + 16*$index
        $width = if ($originalBytes[$entryOffset]) { [int]$originalBytes[$entryOffset] } else { 256 }
        [PSCustomObject]@{ Offset=$entryOffset; Width=$width; Depth=[BitConverter]::ToUInt16($originalBytes,$entryOffset+6) }
    }
    $entry = $entries | Sort-Object @{Expression={[Math]::Abs($_.Width-$Size)}},@{Expression={$_.Depth};Descending=$true} | Select-Object -First 1
    $length = [BitConverter]::ToUInt32($originalBytes,$entry.Offset+8)
    $dataOffset = [BitConverter]::ToUInt32($originalBytes,$entry.Offset+12)
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]1)
        $writer.Write($originalBytes,[int]$entry.Offset,12); $writer.Write([UInt32]22)
        $writer.Write($originalBytes,[int]$dataOffset,[int]$length)
        $writer.Flush(); $stream.Position=0
        $selectedIcon = [Drawing.Icon]::new($stream,$Size,$Size)
        try { return $selectedIcon.ToBitmap() } finally { $selectedIcon.Dispose() }
    } finally { $writer.Dispose() }
}
$preview = Get-OriginalIconBitmap 256
try { $preview.Save((Join-Path $destination 'p1-icon.png'),[Drawing.Imaging.ImageFormat]::Png) }
finally { $preview.Dispose() }
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,96,128,256)) {
    $source = Get-OriginalIconBitmap $size
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size))
        # Keep PCL lettering intact; reserve the lower-right corner for the install badge.
        $scale = $size / 256.0
        $graphics.ScaleTransform([single]$scale,[single]$scale)
        $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
        $blue = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,20,82,166))
        $stroke = [Drawing.Pen]::new([Drawing.Color]::White,7)
        $stroke.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $stroke.EndCap = [Drawing.Drawing2D.LineCap]::Round
        try {
            $graphics.FillEllipse($white,166,166,88,88)
            $graphics.FillEllipse($blue,171,171,78,78)
            $graphics.DrawLine($stroke,210,183,210,215)
            $graphics.FillPolygon($white,[Drawing.Point[]]@([Drawing.Point]::new(195,205),[Drawing.Point]::new(225,205),[Drawing.Point]::new(210,222)))
            $graphics.DrawLines($stroke,[Drawing.Point[]]@([Drawing.Point]::new(188,220),[Drawing.Point]::new(188,231),[Drawing.Point]::new(232,231),[Drawing.Point]::new(232,220)))
        } finally { $stroke.Dispose(); $blue.Dispose(); $white.Dispose() }
        $memory = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
            $frames += [PSCustomObject]@{ Size=$size; Bytes=$memory.ToArray() }
        } finally { $memory.Dispose() }
        if ($size -eq 256) { $bitmap.Save((Join-Path $destination 'p1-installer.png'),[Drawing.Imaging.ImageFormat]::Png) }
    } finally { $graphics.Dispose(); $bitmap.Dispose(); $source.Dispose() }
}
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $destination 'p1-installer.ico')))
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length); $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
[ordered]@{
    originalRepository = 'https://github.com/Meloong-Git/PCL'
    originalCommit = 'e4b0489a22cfd4bc57a18fc1e2ac66d5c18e7fc1'
    originalUrl = 'https://raw.githubusercontent.com/Meloong-Git/PCL/e4b0489a22cfd4bc57a18fc1e2ac66d5c18e7fc1/Plain%20Craft%20Launcher%202/Images/icon.ico'
    originalSha256 = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    launcher = 'p1-launcher.ico'; launcherUse = 'Unmodified original PCL ICO'
    installer = 'p1-installer.ico'; installerChange = 'Original PCL artwork with a lower-right install arrow badge'
    installerSizes = @($frames | ForEach-Object Size)
    attribution = 'PCL original author: 龙腾猫跃. Independent third-party P1 build; not an official release.'
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'icon-sources.json') -Encoding utf8
