[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $projectRoot 'config\download-sources.example.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$headers = @{ 'User-Agent' = $config.modrinth.userAgent; 'Accept' = 'application/json' }
$modFacets = [Uri]::EscapeDataString('[["project_type:mod"]]')
$shaderFacets = [Uri]::EscapeDataString('[["project_type:shader"]]')
$checks = @(
    @{ Name = 'Mojang official manifest'; Url = $config.minecraft.officialManifest; Kind = 'manifest' },
    @{ Name = 'BMCLAPI community mirror manifest'; Url = $config.minecraft.candidateMirrorManifest; Kind = 'manifest' },
    @{ Name = 'Modrinth mod search'; Url = "$($config.modrinth.apiBase)/search?limit=1&facets=$modFacets"; Kind = 'search' },
    @{ Name = 'Modrinth shader search'; Url = "$($config.modrinth.apiBase)/search?limit=1&facets=$shaderFacets"; Kind = 'search' },
    @{ Name = 'Fabric game metadata'; Url = "$($config.fabric.metadataBase)/versions/game"; Kind = 'fabric' }
)
$results = foreach ($check in $checks) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Uri $check.Url -Headers $headers -TimeoutSec 15 -UseBasicParsing
        $payload = $response.Content | ConvertFrom-Json
        $summary = switch ($check.Kind) {
            'manifest' {
                if (-not $payload.latest.release -or @($payload.versions).Count -lt 1) {
                    throw 'Invalid version manifest structure.'
                }
                "Versions: $(@($payload.versions).Count); latest release: $($payload.latest.release)"
            }
            'search' {
                if (-not ($payload.PSObject.Properties.Name -contains 'hits') -or @($payload.hits).Count -lt 1) {
                    throw 'No resource returned by search.'
                }
                "Total hits: $($payload.total_hits); sample: $($payload.hits[0].slug)"
            }
            'fabric' {
                if (@($payload).Count -lt 1 -or -not $payload[0].version) {
                    throw 'Invalid Fabric metadata structure.'
                }
                "Versions: $(@($payload).Count)"
            }
        }
        [PSCustomObject]@{
            name = $check.Name; url = $check.Url; ok = $true
            httpStatus = [int]$response.StatusCode; elapsedMs = $timer.ElapsedMilliseconds
            detail = $summary
        }
    } catch {
        [PSCustomObject]@{
            name = $check.Name; url = $check.Url; ok = $false
            httpStatus = $null; elapsedMs = $timer.ElapsedMilliseconds
            detail = $_.Exception.Message
        }
    } finally {
        $timer.Stop()
    }
}
$reportRoot = Join-Path $projectRoot 'reports'
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$report = [ordered]@{
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    scope = 'Public metadata only; does not verify full download, installation, or launch.'
    checks = @($results)
    notTested = @('CurseForge: project API key is not configured.')
}
$reportPath = Join-Path $reportRoot 'download-source-check.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$results | Format-Table name, ok, httpStatus, elapsedMs, detail -AutoSize
Write-Output "Report: $reportPath"
if (@($results | Where-Object { -not $_.ok }).Count -gt 0) { exit 1 }
