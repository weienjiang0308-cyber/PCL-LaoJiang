[CmdletBinding()]
param([ValidateSet(8,17,21,25)][int]$MajorVersion = 17)
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$url = "https://api.adoptium.net/v3/assets/latest/$MajorVersion/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse"
$packages = Invoke-RestMethod -Uri $url -TimeoutSec 60
$package = $packages[0].binary.package
$archive = Join-Path $projectRoot ('cache\' + $package.name)
if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $package.link -OutFile $archive -TimeoutSec 600 }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.checksum) { throw 'Java runtime checksum mismatch.' }
$destination = Join-Path $projectRoot "sandbox\runtimes\java-$MajorVersion"
if (-not (Test-Path -LiteralPath $destination)) { Expand-Archive -LiteralPath $archive -DestinationPath $destination }
$java = Get-ChildItem -LiteralPath $destination -Filter java.exe -Recurse -File | Select-Object -First 1
if (-not $java) { throw 'Java executable not found after extraction.' }
[ordered]@{ provider = 'Eclipse Adoptium (Temurin)'; metadataUrl = $url; packageUrl = $package.link
    sha256 = $package.checksum; javaPath = $java.FullName
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot "reports\java-$MajorVersion.json") -Encoding utf8
& $java.FullName -version
if ($LASTEXITCODE -ne 0) { throw 'Java failed to run.' }
