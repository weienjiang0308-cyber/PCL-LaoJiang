[CmdletBinding()]
param([string]$Version)
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$metadataUrl = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$metadata = Invoke-RestMethod -Uri $metadataUrl -TimeoutSec 60
if (-not $Version) {
    $sdkLock = Join-Path $projectRoot 'global.json'
    $Version = if (Test-Path -LiteralPath $sdkLock) {
        (Get-Content -LiteralPath $sdkLock -Raw | ConvertFrom-Json).sdk.version
    } else { $metadata.'latest-sdk' }
}
$sdk = @($metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq $Version }) | Select-Object -First 1
if (-not $sdk) { throw "SDK $Version is not listed in Microsoft release metadata." }
$package = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.url.EndsWith('.zip') } | Select-Object -First 1
if (-not $package) { throw 'Windows x64 SDK ZIP not found.' }
$archive = Join-Path $projectRoot "cache\dotnet-sdk-$Version-win-x64.zip"
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri $package.url -OutFile $archive -TimeoutSec 900
}
$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash
if ($actualHash -ne $package.hash) { throw "SDK SHA512 mismatch: $archive" }
if (-not (Test-Path -LiteralPath (Join-Path $env:DOTNET_ROOT "sdk\$Version"))) {
    Expand-Archive -LiteralPath $archive -DestinationPath $env:DOTNET_ROOT -Force
}
[ordered]@{
    version = $Version; architecture = 'win-x64'; metadataUrl = $metadataUrl
    packageUrl = $package.url; sha512 = $actualHash.ToLowerInvariant()
    installDirectory = $env:DOTNET_ROOT; checkedAtUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\dotnet-sdk.json') -Encoding utf8
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') --info
if ($LASTEXITCODE -ne 0) { throw 'Installed SDK failed to run.' }
