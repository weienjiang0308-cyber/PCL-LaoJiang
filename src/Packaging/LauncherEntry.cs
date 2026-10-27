using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PclThirdParty.Packaging
{
    internal static class LauncherEntry
    {
        [STAThread]
        private static int Main(string[] arguments)
        {
            try
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                string executable = Path.Combine(directory, "app", "Plain Craft Launcher 2.exe");
                bool workspace = !File.Exists(executable) && File.Exists(Path.Combine(directory, "artifacts", "launcher", "Plain Craft Launcher 2.exe"));
                if (workspace) executable = Path.Combine(directory, "artifacts", "launcher", "Plain Craft Launcher 2.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("启动器文件缺失，请重新安装 / Launcher files are missing. Please reinstall.", executable);
                string configured = Environment.GetEnvironmentVariable("PCL_THIRDPARTY_DATA_ROOT");
                string data = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(directory, workspace ? @"sandbox\user" : "Data") : Path.GetFullPath(configured);
                string temporary = Path.Combine(data, "temp");
                string bundle = Path.Combine(data, "cache", "dotnet-bundle");
                Directory.CreateDirectory(temporary);
                Directory.CreateDirectory(bundle);
                var start = new ProcessStartInfo(executable, PackageInfo.Arguments(arguments));
                start.WorkingDirectory = directory;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.EnvironmentVariables["PCL_THIRDPARTY_DATA_ROOT"] = data;
                start.EnvironmentVariables["TEMP"] = temporary;
                start.EnvironmentVariables["TMP"] = temporary;
                start.EnvironmentVariables["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = bundle;
                string credentialsFile = Path.Combine(directory, "config", "credentials.local.json");
                if (File.Exists(credentialsFile))
                {
                    var credentials = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(credentialsFile));
                    string value;
                    if (credentials.TryGetValue("microsoftClientId", out value) && !string.IsNullOrWhiteSpace(value))
                        start.EnvironmentVariables["PCL_THIRDPARTY_MS_CLIENT_ID"] = value;
                    if (credentials.TryGetValue("curseforgeApiKey", out value) && !string.IsNullOrWhiteSpace(value))
                        start.EnvironmentVariables["PCL_THIRDPARTY_CURSEFORGE_API_KEY"] = value;
                }
                using (Process process = Process.Start(start)) { process.WaitForExit(); return process.ExitCode; }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "PCL老蒋定制版 P1", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
