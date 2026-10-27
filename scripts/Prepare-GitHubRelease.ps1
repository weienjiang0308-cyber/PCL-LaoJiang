[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-manifest.json') -Raw | ConvertFrom-Json
$acceptance = Get-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-p1-acceptance.json') -Raw | ConvertFrom-Json
$installer = Join-Path $projectRoot 'artifacts\PCL老蒋定制版-P1-安装.exe'
$installerHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
if ($installerHash -ne $manifest.installerSha256 -or
    $installerHash -ne $acceptance.installerSha256 -or $acceptance.status -ne 'passed') {
    throw 'The current installer does not match its passing acceptance report.'
}
& python (Join-Path $PSScriptRoot 'Export-Source.py')
if ($LASTEXITCODE -ne 0) { throw 'Source export failed.' }
$assetNames = @('PCL老蒋定制版-P1-安装.exe','PCL老蒋定制版-P1-便携版.zip','PCL老蒋定制版-P1-源码.zip')
$assets = foreach ($name in $assetNames) {
    $path = Join-Path $projectRoot "artifacts\$name"
    $file = Get-Item -LiteralPath $path
    [ordered]@{name=$name;path=$path;bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$checksumFile = Join-Path $projectRoot 'artifacts\SHA256SUMS.txt'
$checksumText = (@($assets | ForEach-Object { "$($_.sha256)  $($_.name)" }) -join "`n") + "`n"
[IO.File]::WriteAllText($checksumFile,$checksumText,[Text.UTF8Encoding]::new($false))
# Use stable ASCII attachment names on GitHub while retaining the Chinese names
# inside the package and in the local artifacts directory.
$releaseDirectory = Join-Path $projectRoot 'work\github-release'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$releaseNames = @('PCL-LaoJiang-P1-Setup.exe','PCL-LaoJiang-P1-Portable.zip','PCL-LaoJiang-P1-Source.zip')
$releaseAssets = for ($index=0; $index -lt $assets.Count; $index++) {
    $asset = $assets[$index]
    $target = Join-Path $releaseDirectory $releaseNames[$index]
    Copy-Item -LiteralPath $asset.path -Destination $target
    [ordered]@{name=$releaseNames[$index];label=$asset.name;path=$target;bytes=$asset.bytes;sha256=$asset.sha256}
}
$releaseChecksums = Join-Path $releaseDirectory 'SHA256SUMS.txt'
$releaseChecksumText = (@($releaseAssets | ForEach-Object { "$($_.sha256)  $($_.name)" }) -join "`n") + "`n"
[IO.File]::WriteAllText($releaseChecksums,$releaseChecksumText,[Text.UTF8Encoding]::new($false))
[ordered]@{
    status='prepared-not-published';product='PCL老蒋定制版';tag='p1';prerelease=$true
    notes='docs/releases/P1.md';installerAcceptance='passed';assets=@($releaseAssets);checksums=$releaseChecksums
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\github-release-preparation.json') -Encoding utf8
Write-Output 'P1 release attachments prepared and installer acceptance hash verified.'
