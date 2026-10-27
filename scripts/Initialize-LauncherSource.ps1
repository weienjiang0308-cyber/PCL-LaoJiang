[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $projectRoot 'upstream-lock.json') -Raw | ConvertFrom-Json
$reference = Join-Path $projectRoot $lock.candidateUpstream.relativePath
$commit = (& git -C $reference rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -ne $lock.candidateUpstream.commit) { throw 'Reference commit differs from upstream-lock.json.' }
if (& git -C $reference status --porcelain) { throw 'Reference checkout is modified.' }
$destination = Join-Path $projectRoot 'src\PCL-ThirdParty'
if (Test-Path -LiteralPath $destination) { throw "Source already exists: $destination" }
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'temp'),$destination -Force | Out-Null
$archive = Join-Path $projectRoot 'temp\pcl-reference.zip'
& git -C $reference archive --format=zip "--output=$archive" $commit
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destination
Write-Output "Created independent source snapshot at $destination ($commit)."
