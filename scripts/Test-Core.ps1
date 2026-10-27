[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$log = Join-Path $projectRoot 'reports\test-core-selected.log'
& "$env:DOTNET_ROOT\dotnet.exe" test (Join-Path $projectRoot 'src\PCL-ThirdParty\PCL.Core.Test\PCL.Core.Test.csproj') `
    -c Debug -p:Platform=x64 --filter 'FullyQualifiedName~LocalizationTest|FullyQualifiedName~ThirdPartySourceTests|FullyQualifiedName~ModDependencyResolverTest' `
    --logger 'trx;LogFileName=core-selected.trx' --results-directory (Join-Path $projectRoot 'reports\tests') *> $log
$testExit = $LASTEXITCODE
Get-Content -LiteralPath $log -Tail 15
if ($testExit -ne 0) { throw "Core tests failed. See $log" }
