[CmdletBinding()]
param()
. "$PSScriptRoot\Enter-ProjectEnvironment.ps1"
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler is required.' }
$core = Join-Path $projectRoot 'artifacts\launcher\Plain Craft Launcher 2.exe'
if (-not (Test-Path -LiteralPath $core)) { throw 'Publish the Release launcher first.' }
$packaging = Join-Path $projectRoot 'src\Packaging'
$stage = Join-Path $projectRoot 'work\installer-package'
$launcherIcon = Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\Images\Branding\p1-launcher.ico'
$installerIcon = Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\Images\Branding\p1-installer.ico'
New-Item -ItemType Directory -Path $stage,(Join-Path $stage 'app'),(Join-Path $stage 'config') -Force | Out-Null
$common = Join-Path $packaging 'PackagingCommon.cs'
$compilerOptions = @('/nologo','/target:winexe','/platform:x64','/optimize+', '/utf8output',
    "/win32manifest:$(Join-Path $packaging 'app.manifest')",
    '/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll',
    '/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
function Compile-Application([string]$OutputPath, [string[]]$Sources, [string[]]$AdditionalOptions, [string]$ApplicationIcon) {
    $compilerLog = Join-Path $projectRoot ('reports\compile-' + [IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.log')
    & $compiler @compilerOptions "/out:$OutputPath" "/win32icon:$ApplicationIcon" @AdditionalOptions @Sources *> $compilerLog
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $compilerLog; throw "Compilation failed: $OutputPath" }
}
Compile-Application -OutputPath (Join-Path $stage 'PCL老蒋定制版.exe') -Sources @($common,(Join-Path $packaging 'LauncherEntry.cs')) -AdditionalOptions @() -ApplicationIcon $launcherIcon
Compile-Application -OutputPath (Join-Path $stage 'Uninstall.exe') -Sources @($common,(Join-Path $packaging 'Setup.cs')) -AdditionalOptions @('/define:UNINSTALL',"/resource:$launcherIcon,AppIcon") -ApplicationIcon $launcherIcon
Copy-Item -LiteralPath $core -Destination (Join-Path $stage 'app\Plain Craft Launcher 2.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\LICENCE') -Destination (Join-Path $stage 'PCL-LICENCE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\LICENSE') -Destination (Join-Path $stage 'LICENSE-directory-scopes.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\THIRD-PARTY-CHANGES.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\PCL-ThirdParty\Plain Craft Launcher 2\metadata.json') -Destination (Join-Path $stage 'component-notices.json')
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\credentials.example.json') -Destination (Join-Path $stage 'config\credentials.example.json')
@'
Plain Craft Launcher (PCL) 老蒋定制版 P1
第三方独立开发预览版 / Independent third-party development preview

双击 PCL老蒋定制版.exe 启动，无需 CMD、PowerShell 或单独安装 .NET 10。
Launch PCL老蒋定制版.exe directly; no CMD, PowerShell or separate .NET 10 installation is needed.

游戏、存档、账号和缓存保存在本目录的 Data 文件夹。卸载会保留 Data。
Game files, saves, accounts and caches are in Data. Uninstall keeps Data.
可选凭证配置：将 config/credentials.example.json 复制为 config/credentials.local.json。
Optional credentials: copy config/credentials.example.json to config/credentials.local.json.

支持简体中文 / English (US) / English (United Kingdom)，可在启动器设置中切换。
Select Chinese or English in the launcher settings.
微软正版登录和 CurseForge 尚需项目自己的应用身份/API Key；当前未验收。
Microsoft account sign-in and CurseForge need project-owned configuration and have not been verified.

原作者：龙腾猫跃。社区基础：PCL Community / PCL CE v2.15.1。
并非 PCL 原作者或 PCL Community 发布的官方版本。
Not an official release by the PCL or PCL CE maintainers.
请保留并阅读随附许可和第三方声明 / Retain and read the accompanying licenses and notices.
'@ | Set-Content -LiteralPath (Join-Path $stage 'READ-ME-FIRST.txt') -Encoding utf8
$owned = @('PCL老蒋定制版.exe','Uninstall.exe','app/Plain Craft Launcher 2.exe','PCL-LICENCE.txt',
    'LICENSE-directory-scopes.txt','THIRD-PARTY-CHANGES.md','component-notices.json','READ-ME-FIRST.txt','config/credentials.example.json')
$hashes = [ordered]@{}
foreach ($file in $owned) { $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $stage $file) -Algorithm SHA256).Hash }
$hashPath = Join-Path $projectRoot 'work\installer-file-hashes.json'
$hashes | ConvertTo-Json | Set-Content -LiteralPath $hashPath -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $projectRoot 'work\installer-payload.zip'
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
$archive = [IO.Compression.ZipFile]::Open($payload,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $owned) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $stage $file),$file,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$setup = Join-Path $projectRoot 'artifacts\PCL老蒋定制版-P1-安装.exe'
Compile-Application -OutputPath $setup -Sources @($common,(Join-Path $packaging 'Setup.cs')) -AdditionalOptions @("/resource:$installerIcon,AppIcon","/resource:$payload,PackagePayload","/resource:$hashPath,PackageHashes") -ApplicationIcon $installerIcon
Copy-Item -LiteralPath (Join-Path $stage 'PCL老蒋定制版.exe') -Destination (Join-Path $projectRoot 'PCL老蒋定制版.exe')
$portable = Join-Path $projectRoot 'artifacts\PCL老蒋定制版-P1-便携版.zip'
Copy-Item -LiteralPath $payload -Destination $portable
[ordered]@{
    product = 'PCL老蒋定制版'; displayVersion = 'P1'; internalVersion = '1.0.0-p1'
    launcherIcon = $launcherIcon; installerIcon = $installerIcon
    status = 'built-local-development-preview'; installer = $setup
    installerSha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
    entryExecutable = (Join-Path $projectRoot 'PCL老蒋定制版.exe'); portableArchive = $portable
    defaultInstallDirectory = 'D:\Games\PCL-LaoJiang'; adminRequired = $false
    entryRuntime = '.NET Framework 4.8 supplied by Windows'; launcherRuntime = 'self-contained .NET 10 win-x64'
    installedFiles = $owned; fileHashes = $hashes; preservesUserDataOnUninstall = $true
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $projectRoot 'reports\installer-manifest.json') -Encoding utf8
# Keep superseded packages out of the release folder so users cannot mistake an
# old installer (and its old icon) for the current P1 build. Retain a local backup.
& "$PSScriptRoot\Archive-LegacyArtifacts.ps1"
Write-Output "Installer: $setup"
Write-Output "EXE entry: $(Join-Path $projectRoot 'PCL老蒋定制版.exe')"
