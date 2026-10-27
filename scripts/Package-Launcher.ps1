[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$outputDirectory = Join-Path $projectRoot 'artifacts\launcher'
$executable = Join-Path $outputDirectory 'Plain Craft Launcher 2.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Publish the Release launcher first.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\LICENCE') -Destination (Join-Path $outputDirectory 'PCL-LICENCE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\LICENSE') -Destination (Join-Path $outputDirectory 'LICENSE-directory-scopes.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\THIRD-PARTY-CHANGES.md') -Destination $outputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\metadata.json') -Destination (Join-Path $outputDirectory 'component-notices.json')
$notes = @'
Plain Craft Launcher (PCL) ThirdParty Community Fork
Independent third-party local development preview, based on PCL CE v2.15.1.
Original author: 龙腾猫跃. Community base: PCL Community.

本目录仅包含启动器，不包含游戏、Java、模组、光影、账号或凭证。
建议从工作区根目录双击 Start-Launcher.cmd，以使用 D 盘独立沙盒和项目凭证配置。
直接运行 EXE 时，数据默认写入 EXE 旁的 ThirdPartyData 目录。
语言可在设置中选择简体中文或 English；来源在游戏管理设置中选择。
正版登录与 CurseForge 需要项目自己的应用配置。没有配置独立在线更新服务。

This directory contains the launcher only, without games, Java, mods, shader packs, accounts or credentials.
Use Start-Launcher.cmd in the workspace to use the D: sandbox and project-local credentials.
When run directly, this EXE stores data under ThirdPartyData next to the EXE.
Select Chinese / English in Settings and choose providers in game management settings.
Microsoft sign-in and CurseForge need this project's own application configuration.
No independent online update service has been configured.

See the workspace reports/acceptance-*.json for exact verification scope and outstanding work.
Read PCL-LICENCE.txt, LICENSE-directory-scopes.txt and component-notices.json.
Public distribution clearance, signing and a public source release are still outstanding.
'@
$notes | Set-Content -LiteralPath (Join-Path $outputDirectory 'READ-ME-FIRST.txt') -Encoding utf8
$archive = Join-Path $projectRoot 'artifacts\PCL-ThirdParty-dev-win-x64.zip'
$packageFiles = @('Plain Craft Launcher 2.exe','PCL-LICENCE.txt','LICENSE-directory-scopes.txt','THIRD-PARTY-CHANGES.md','component-notices.json','READ-ME-FIRST.txt')
$paths = $packageFiles | ForEach-Object { Join-Path $outputDirectory $_ }
Compress-Archive -LiteralPath $paths -DestinationPath $archive -Force
[ordered]@{
    product = 'Plain Craft Launcher (PCL) ThirdParty Community Fork'
    status = 'local-development-preview'; runtime = 'self-contained-win-x64'
    executable = $executable; executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    archive = $archive; archiveSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    packageFiles = $packageFiles; sourceDirectory = 'src/PCL-ThirdParty'
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\package-manifest.json') -Encoding utf8
Write-Output "Package: $archive"
