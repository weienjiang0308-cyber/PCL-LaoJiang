[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$backupRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'work\legacy-artifacts'))
$legacyNames = @('PCL-ThirdParty-Setup.exe','PCL-ThirdParty-portable-win-x64.zip',
    'PCL-ThirdParty-dev-win-x64.zip','PCL-ThirdParty-dev-source.zip')
$legacyFiles = @($legacyNames | ForEach-Object {
    $candidate = Join-Path $artifactRoot $_
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { Get-Item -LiteralPath $candidate }
})
if ($legacyFiles.Count -eq 0) { return }
# Archive only after the current release exists.
$current = Join-Path $artifactRoot 'PCL老蒋定制版-P1-安装.exe'
if (-not (Test-Path -LiteralPath $current -PathType Leaf)) { throw 'Build the P1 installer before archiving legacy artifacts.' }
$destination = [IO.Path]::GetFullPath((Join-Path $backupRoot (Get-Date -Format 'yyyyMMdd-HHmmssfff')))
if (-not $destination.StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Legacy backup destination is outside the project backup directory.'
}
New-Item -ItemType Directory -Path $destination | Out-Null
$records = foreach ($file in $legacyFiles) {
    $source = [IO.Path]::GetFullPath($file.FullName)
    if (-not $source.StartsWith($artifactRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Legacy artifact is outside the release directory.'
    }
    $target = Join-Path $destination $file.Name
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Move-Item -LiteralPath $source -Destination $target
    [ordered]@{previousPath=$source;backupPath=$target;sha256=$hash}
}
[ordered]@{status='archived';currentInstaller=$current;files=@($records)} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\legacy-artifacts-archive.json') -Encoding utf8
Write-Output "Legacy packages archived: $destination"
