$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_ROOT = Join-Path $projectRoot 'work\tools\dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'cache\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot 'cache\nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot 'cache\nuget\http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $projectRoot 'cache\nuget\plugins'
$env:TEMP = Join-Path $projectRoot 'temp'
$env:TMP = $env:TEMP
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $projectRoot 'cache\dotnet-bundle'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'reports'),(Join-Path $projectRoot 'artifacts') -Force | Out-Null
foreach ($directory in @($env:DOTNET_ROOT, $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES,
    $env:NUGET_HTTP_CACHE_PATH, $env:NUGET_PLUGINS_CACHE_PATH, $env:TEMP,
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
