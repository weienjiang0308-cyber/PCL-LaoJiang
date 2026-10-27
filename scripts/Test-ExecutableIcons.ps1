[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
Add-Type -AssemblyName System.Drawing
if (-not ('PclIconResourceVerification' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PclIconResourceVerification {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern uint PrivateExtractIcons(string file, int index, int width, int height,
        IntPtr[] handles, uint[] identifiers, uint count, uint flags);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
}
'@
}
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-manifest.json') -Raw | ConvertFrom-Json
$iconDirectory = Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\Images\Branding'
$checks = @(
    @{Name='launcher';Executable=$manifest.entryExecutable;Expected='p1-icon.png'},
    @{Name='installer';Executable=$manifest.installer;Expected='p1-installer.png'},
    @{Name='core';Executable=(Join-Path $projectRoot 'artifacts\launcher\Plain Craft Launcher 2.exe');Expected='p1-icon.png'}
)
$results = foreach ($check in $checks) {
    $handles = [IntPtr[]]::new(1)
    $identifiers = [UInt32[]]::new(1)
    $count = [PclIconResourceVerification]::PrivateExtractIcons($check.Executable,0,256,256,$handles,$identifiers,1,0)
    if ($count -ne 1 -or $handles[0] -eq [IntPtr]::Zero) { throw "EXE has no extractable icon: $($check.Name)" }
    $icon = [Drawing.Icon]::FromHandle($handles[0])
    $bitmap = $icon.ToBitmap()
    $expected = [Drawing.Bitmap]::new((Join-Path $iconDirectory $check.Expected))
    try {
        if ($bitmap.Width -ne 256 -or $bitmap.Height -ne 256) { throw "Missing 256px icon frame: $($check.Name)" }
        $tested=0; $matched=0
        for ($y=0; $y -lt 256; $y++) {
            for ($x=0; $x -lt 256; $x++) {
                $reference = $expected.GetPixel($x,$y)
                if ($reference.A -lt 250) { continue }
                $actual = $bitmap.GetPixel($x,$y)
                $tested++
                if ([Math]::Abs([int]$reference.R-$actual.R) -le 8 -and
                    [Math]::Abs([int]$reference.G-$actual.G) -le 8 -and
                    [Math]::Abs([int]$reference.B-$actual.B) -le 8 -and $actual.A -ge 240) { $matched++ }
            }
        }
        $ratio = $matched / [Math]::Max(1,$tested)
        if ($ratio -lt 0.99) { throw "EXE icon does not match the selected artwork: $($check.Name) ($ratio)" }
        $output = Join-Path $projectRoot ("reports\p1-$($check.Name)-exe-icon.png")
        $bitmap.Save($output,[Drawing.Imaging.ImageFormat]::Png)
        [PSCustomObject]@{name=$check.Name;status='passed';executable=$check.Executable;preview=$output;pixelMatchRatio=$ratio}
    } finally {
        $expected.Dispose(); $bitmap.Dispose(); $icon.Dispose()
        [PclIconResourceVerification]::DestroyIcon($handles[0]) | Out-Null
    }
}
$originalHash = (Get-FileHash -LiteralPath (Join-Path $iconDirectory 'pcl-original.ico') -Algorithm SHA256).Hash
$launcherHash = (Get-FileHash -LiteralPath (Join-Path $iconDirectory 'p1-launcher.ico') -Algorithm SHA256).Hash
if ($originalHash -ne $launcherHash) { throw 'Launcher ICO differs from the original PCL ICO.' }
[ordered]@{status='passed';launcherRetainsOriginal=$true;checks=@($results)} | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $projectRoot 'reports\executable-icon-check.json') -Encoding utf8
$results | Select-Object name,status,pixelMatchRatio | ConvertTo-Json
