using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>Sandbox acceptance runner using the desktop install pipeline.</summary>
internal static class WorkspaceVerification
{
    public static bool Active => Basics.CommandLineArguments.Contains("--workspace-verify");
    private static bool _started;
    private static readonly Dictionary<string, object?> Report = new();
    private static string ReportPath => Path.Combine(ThirdPartyDistribution.DataRoot, "verification.json");

    private static string? Option(string name)
    {
        var args = Basics.CommandLineArguments;
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void Stage(string name, object? result = null)
    {
        Directory.CreateDirectory(ThirdPartyDistribution.DataRoot);
        Report["stage"] = name;
        Report["updatedAtUtc"] = DateTime.UtcNow;
        if (result is not null) Report[name] = result;
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(Report, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task RunAsync(FormMain form)
    {
        if (_started) return;
        _started = true;
        var exitCode = 1;
        try
        {
            var configured = Environment.GetEnvironmentVariable("PCL_THIRDPARTY_DATA_ROOT");
            if (string.IsNullOrWhiteSpace(configured) || !Path.GetFullPath(configured).Contains("sandbox", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Verification requires an explicit sandbox data directory.");
            Stage("started", new { dataRoot = ThirdPartyDistribution.DataRoot, version = Basics.VersionName });
            await Task.Delay(1800);
            form.WindowState = WindowState.Minimized;
            var original = "https://piston-data.mojang.com/v1/objects/example/client.jar";
            if (DownloadSourceRouter.GetMirrorUrl(original) != "https://bmclapi2.bangbang93.com/v1/objects/example/client.jar" ||
                DownloadSourceRouter.GetMirrorUrl("https://piston-data.mojang.com.evil.invalid/v1/objects/a") is not null ||
                DownloadSourceRouter.GetMirrorUrl("https://login.microsoftonline.com/consumers/oauth2/v2.0/token") is not null ||
                DownloadSourceRouter.GetUrls(original, 2).Count() != 1)
                throw new InvalidOperationException("Source routing verification failed.");
            Stage("source-routing", "Passed: exact hosts, no authentication rewrite, original-only mode.");
            await DownloadFailureVerification.RunAsync();
            Stage("download-failure-handling", "Passed: 403 not retried; wrong hash rejected; partial files removed.");
            foreach (var language in new[] { "zh-CN", "en-US", "en-GB" })
            {
                LocalizationService.Apply(language);
                var text = Lang.Text("ThirdParty.Notice.Title");
                if (text.Contains("ThirdParty.Notice.Title")) throw new InvalidOperationException("Missing translation.");
                Capture(new PageSetupAbout(), "about-" + language);
                Capture(new PageSetupGameManage(), "sources-" + language);
                Capture(new PageSetupLauncherLanguage(), "language-" + language);
                Capture(new PageLaunchRight(), "home-" + language);
                if (language != "en-GB")
                {
                    form.WindowState = WindowState.Normal;
                    form.Left = -4000;
                    form.Top = 0;
                    form.Width = 1120;
                    form.Height = 730;
                    await Task.Delay(400);
                    form.UpdateLayout();
                    CaptureWindow(form, "window-" + language);
                }
                Stage("language-" + language, text);
            }
            LocalizationService.Apply("en-US");
            if (RestrictedFeaturePolicy.IsRestricted) throw new InvalidOperationException("English incorrectly disables downloads.");
            var version = Option("--install-version");
            if (version is not null)
            {
                var source = Option("--source") == "mirror" ? 0 : 2;
                await Task.Run(() => Install(version, source, Option("--fabric-version"))).WaitAsync(TimeSpan.FromMinutes(20));
            }
            Report["gameLaunchVerified"] = false;
            Report["microsoftLoginVerified"] = false;
            Report["curseforgeVerified"] = false;
            var demoInstance = Option("--demo-instance");
            if (demoInstance is not null) await LaunchDemoAsync(demoInstance);
            Stage("passed");
            exitCode = 0;
        }
        catch (Exception exception) { Stage("failed", exception.ToString()); }
        finally { System.Windows.Application.Current.Shutdown(exitCode); }
    }

    private static async Task LaunchDemoAsync(string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new ArgumentException("Invalid instance name.");
        var javaPath = Environment.GetEnvironmentVariable("PCL_THIRDPARTY_JAVA_PATH");
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            throw new InvalidOperationException("A project Java runtime is required for the demo launch test.");
        var gameRoot = Path.Combine(ThirdPartyDistribution.DataRoot, ".minecraft") + Path.DirectorySeparatorChar;
        ModFolder.mcFolderSelected = gameRoot;
        var target = Path.Combine(gameRoot, "versions", name) + Path.DirectorySeparatorChar;
        var instance = new McInstance(target);
        instance.Load();
        var verifyWorld = Basics.CommandLineArguments.Contains("--verify-world");
        if (verifyWorld)
        {
            var optionsPath = Path.Combine(instance.PathIndie, "options.txt");
            var lines = File.Exists(optionsPath) ? File.ReadAllLines(optionsPath).ToList() : new List<string>();
            lines.RemoveAll(line => line.StartsWith("guiScale:") || line.StartsWith("onboardAccessibility:") || line.StartsWith("renderDistance:"));
            lines.AddRange(new[] { "guiScale:2", "onboardAccessibility:false", "renderDistance:6" });
            File.WriteAllLines(optionsPath, lines);
        }
        var shaderDirectory = Path.Combine(instance.PathIndie, "shaderpacks");
        if (Directory.Exists(shaderDirectory))
        {
                var requestedShader = Option("--shader-file");
                if (requestedShader is not null && Path.GetFileName(requestedShader) != requestedShader)
                    throw new ArgumentException("Shader filename must not contain a directory.");
                var shader = requestedShader is not null ? Path.Combine(shaderDirectory, requestedShader) : Directory.GetFiles(shaderDirectory, "*.zip").FirstOrDefault();
            if (shader is not null)
            {
                Directory.CreateDirectory(Path.Combine(instance.PathIndie, "config"));
                File.WriteAllText(Path.Combine(instance.PathIndie, "config", "iris.properties"),
                    "enableShaders=true\nshaderPack=" + Path.GetFileName(shader) + "\n");
            }
        }
        Config.Launch.SelectedJava = javaPath;
        Config.Launch.SetGpuPreference = false;
        Config.Instance.MemorySolution[target] = 1;
        Config.Instance.CustomMemorySize[target] = 20;
        ProfileService.Add(new McProfile
        {
            ProfileId = Guid.NewGuid().ToString("N"), UserName = "SandboxDemo",
            Uuid = "00000000000030008000000000000000", AccessToken = "0", ProfileType = ProfileType.Offline
        });
        var options = new ModLaunch.McLaunchOptions
        {
            instance = instance, ExtraArgs = new() { "--demo" }
        };
        Stage("demo-launching", new { name, javaPath, mode = "official-demo" });
        try
        {
            if (!ModLaunch.McLaunchStart(options)) throw new InvalidOperationException("Launcher declined the demo request.");
            await Task.Run(() => ModLaunch.mcLaunchLoader.WaitForExit(options)).WaitAsync(TimeSpan.FromMinutes(4));
            var process = ModLaunch.mcLaunchProcess;
            if (process is null || process.HasExited) throw new InvalidOperationException("Demo game process exited before verification.");
            var logPath = Path.Combine(instance.PathIndie, "logs", "latest.log");
            var ready = false;
            string log = "";
            for (var attempt = 0; attempt < 90; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("Demo exited during startup.");
                if (File.Exists(logPath))
                {
                    using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    log = await reader.ReadToEndAsync();
                    if (log.Contains("OpenAL initialized", StringComparison.OrdinalIgnoreCase) &&
                        log.Contains("Created:", StringComparison.OrdinalIgnoreCase)) { ready = true; break; }
                }
                await Task.Delay(1000);
            }
            if (!ready) throw new InvalidOperationException("Demo renderer readiness was not confirmed.");
            Stage("demo-started", new { processId = process.Id, logPath, rendererReady = true, paidAccountVerified = false,
                irisLoaded = log.Contains("Iris", StringComparison.OrdinalIgnoreCase),
                shaderPackReported = log.Contains("Using shaderpack", StringComparison.OrdinalIgnoreCase),
                shaderInitializationWarnings = log.Contains("Unknown variable", StringComparison.OrdinalIgnoreCase) || log.Contains("disabling shaders", StringComparison.OrdinalIgnoreCase) });
            Report["demoLaunchVerified"] = true;
            if (verifyWorld)
            {
                // An external UI driver (or the tester) clicks Play Demo World in the verified game window.
                // Do not synthesize cursor messages: GLFW reads the actual cursor position.
                Stage("demo-world-awaiting-click", "Click Play Demo World in this sandbox game window.");
                var joined = false;
                for (var attempt = 0; attempt < 150; attempt++)
                {
                    if (process.HasExited) throw new InvalidOperationException("Demo exited while creating the world.");
                    using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    log = await reader.ReadToEndAsync();
                    if (log.Contains("logged in with entity id", StringComparison.OrdinalIgnoreCase)) { joined = true; break; }
                    await Task.Delay(1000);
                }
                if (!joined) throw new InvalidOperationException("Demo world entry was not confirmed.");
                await Task.Delay(10000);
                using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream)) log = await reader.ReadToEndAsync();
                var shaderFailure = log.Contains("Unknown variable", StringComparison.OrdinalIgnoreCase) ||
                    log.Contains("disabling shaders", StringComparison.OrdinalIgnoreCase) || log.Contains("ShaderCompileException", StringComparison.OrdinalIgnoreCase);
                if (shaderFailure) throw new InvalidOperationException("Shader errors occurred in the demo world.");
                Stage("demo-world-verified", new { name, logPath, shaderErrors = false, worldEntered = true,
                    shaderPackReported = log.Contains("Using shaderpack", StringComparison.OrdinalIgnoreCase) });
                Report["demoWorldVerified"] = true;
            }
        }
        finally
        {
            var process = ModLaunch.mcLaunchProcess;
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                for (var attempt = 0; attempt < 30 && !process.HasExited; attempt++) await Task.Delay(100);
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
    }

    private static void Capture(FrameworkElement element, string name)
    {
        element.Width = 1040;
        element.Height = 760;
        element.Measure(new Size(1040, 760));
        element.Arrange(new Rect(0, 0, 1040, 760));
        element.UpdateLayout();
        var image = new RenderTargetBitmap(1040, 760, 96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(ThirdPartyDistribution.DataRoot, name + ".png"));
        encoder.Save(stream);
    }

    private static void CaptureWindow(FormMain form, string name)
    {
        var bitmap = new RenderTargetBitmap((int)form.ActualWidth, (int)form.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(form);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(ThirdPartyDistribution.DataRoot, name + ".png"));
        encoder.Save(stream);
    }

    private static void Install(string version, int source, string? fabricVersion)
    {
        Config.Download.FileSource = source;
        Config.Download.VersionListSource = source;
        Config.Download.ThreadLimit = 15;
        Config.Download.FileConnectionLimit = 1;
        var gameRoot = Path.Combine(ThirdPartyDistribution.DataRoot, ".minecraft") + Path.DirectorySeparatorChar;
        ModFolder.mcFolderSelected = gameRoot;
        Directory.CreateDirectory(Path.Combine(gameRoot, "versions"));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ThirdPartyDistribution.UserAgent);
        var manifestUrl = DownloadSourceRouter.GetUrls("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", source).First();
        var manifest = JsonNode.Parse(client.GetStringAsync(manifestUrl).GetAwaiter().GetResult())!;
        var entry = manifest["versions"]!.AsArray().First(node => node!["id"]!.GetValue<string>() == version)!;
        if (fabricVersion == "auto")
        {
            var fabricMetadata = JsonNode.Parse(client.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader/" + Uri.EscapeDataString(version)).GetAwaiter().GetResult())!.AsArray();
            fabricVersion = fabricMetadata.First(node => node!["loader"]!["stable"]!.GetValue<bool>())!["loader"]!["version"]!.GetValue<string>();
        }
        var name = fabricVersion is null ? version : version + "-fabric-" + fabricVersion;
        var target = Path.Combine(gameRoot, "versions", name) + Path.DirectorySeparatorChar;
        Stage("installing", new { version, fabricVersion, source = DownloadSourceRouter.GetSourceName(manifestUrl), target });
        var request = new ModDownloadLib.McInstallRequest
        {
            minecraftName = version, minecraftJson = entry["url"]!.GetValue<string>(),
            targetInstanceName = name, targetInstanceFolder = target, fabricVersion = fabricVersion
        };
        var loaders = ModDownloadLib.McInstallLoader(request);
        if (loaders is null) throw new InvalidOperationException("Installer declined the request.");
        var installer = new ModLoader.LoaderCombo<string>("Workspace installation " + name, loaders);
        installer.WaitForExit(target);
        if (!File.Exists(Path.Combine(target, name + ".json")) || File.Exists(Path.Combine(target, ".pclignore")))
            throw new InvalidOperationException("Installer did not finalize the instance.");
        Stage("installed", new
        {
            name, target, libraries = Directory.GetFiles(Path.Combine(gameRoot, "libraries"), "*", SearchOption.AllDirectories).Length,
            assets = Directory.GetFiles(Path.Combine(gameRoot, "assets", "objects"), "*", SearchOption.AllDirectories).Length
        });
        if (fabricVersion is not null)
        {
            var instance = new McInstance(target);
            instance.Load();
            var resourceRoot = instance.PathIndie;
            var resources = new List<object>();
            var resolved = new Dictionary<string, string>();
            JsonObject SelectVersion(string project, string? loader)
            {
                var url = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(project) + "/version?game_versions=" +
                          Uri.EscapeDataString(JsonSerializer.Serialize(new[] { version }));
                if (loader is not null) url += "&loaders=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { loader }));
                var versions = JsonNode.Parse(client.GetStringAsync(url).GetAwaiter().GetResult())!.AsArray();
                return (JsonObject)(versions.FirstOrDefault(node => node!["version_type"]!.GetValue<string>() == "release") ?? versions.First()
                    ?? throw new InvalidOperationException("No compatible resource: " + project));
            }
            void DownloadVersion(JsonObject metadata, ModComp.CompType type, string subdirectory)
            {
                var projectId = metadata["project_id"]!.GetValue<string>();
                var versionId = metadata["id"]!.GetValue<string>();
                if (resolved.TryGetValue(projectId, out var previous))
                {
                    if (previous != versionId) throw new InvalidOperationException("Conflicting required dependency versions: " + projectId);
                    return;
                }
                if (!metadata["game_versions"]!.AsArray().Any(node => node!.GetValue<string>() == version))
                    throw new InvalidOperationException("Required resource does not match the game version.");
                resolved.Add(projectId, versionId);
                foreach (var dependency in metadata["dependencies"]!.AsArray())
                {
                    if (dependency!["dependency_type"]!.GetValue<string>() != "required") continue;
                    var dependencyVersionId = dependency["version_id"]?.GetValue<string>();
                    var dependencyMetadata = dependencyVersionId is not null
                        ? (JsonObject)JsonNode.Parse(client.GetStringAsync("https://api.modrinth.com/v2/version/" + Uri.EscapeDataString(dependencyVersionId)).GetAwaiter().GetResult())!
                        : SelectVersion(dependency["project_id"]!.GetValue<string>(), "fabric");
                    DownloadVersion(dependencyMetadata, ModComp.CompType.Mod, "mods");
                }
                var file = new ModComp.CompFile(metadata, type);
                var directory = Path.Combine(resourceRoot, subdirectory) + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(directory);
                var download = file.ToNetFile(directory, ModComp.DownloadReason.Standalone, version, ModComp.CompLoaderType.Fabric);
                Stage("resource-downloading", new { projectId, versionId, directory });
                new LoaderDownload("Workspace Modrinth " + projectId, new() { download }).WaitForExit();
                resources.Add(new { projectId, versionId, type = type.ToString(), file = download.LocalPath, hash = file.Hash });
            }
            Stage("resources-resolving");
            DownloadVersion(SelectVersion("fabric-api", "fabric"), ModComp.CompType.Mod, "mods");
            DownloadVersion(SelectVersion("iris", "fabric"), ModComp.CompType.Mod, "mods");
            DownloadVersion(SelectVersion("complementary-reimagined", null), ModComp.CompType.Shader, "shaderpacks");
            DownloadVersion(SelectVersion("faithful-32x", null), ModComp.CompType.ResourcePack, "resourcepacks");
            Stage("resources-installed", resources);
        }
    }
}
