using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

[assembly: System.Reflection.AssemblyTitle("PCL老蒋定制版 P1")]
[assembly: System.Reflection.AssemblyProduct("PCL老蒋定制版")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("P1")]

namespace PclThirdParty.Packaging
{
    internal static class PackageInfo
    {
        internal const string AppId = "PCLLaoJiang.CustomEdition";
        internal const string Name = "Plain Craft Launcher (PCL) 老蒋定制版";
        internal const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PCLLaoJiang.CustomEdition";
        internal static readonly string[] OwnedFiles = {
            "PCL老蒋定制版.exe", "Uninstall.exe", "app/Plain Craft Launcher 2.exe",
            "PCL-LICENCE.txt", "LICENSE-directory-scopes.txt", "THIRD-PARTY-CHANGES.md",
            "component-notices.json", "READ-ME-FIRST.txt", "config/credentials.example.json"
        };

        internal static string Quote(string text)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in text)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1).Append(character);
                else result.Append('\\', slashes).Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        internal static string Arguments(IEnumerable<string> arguments)
        {
            var quoted = new List<string>();
            foreach (string argument in arguments) quoted.Add(Quote(argument));
            return string.Join(" ", quoted.ToArray());
        }

        internal static string NormalizeDirectory(string path)
        {
            if (!Path.IsPathRooted(path)) throw new InvalidOperationException("请选择绝对路径 / Choose an absolute directory.");
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (normalized.Length < 4 || string.Equals(normalized, Path.GetPathRoot(path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不能安装到磁盘根目录 / Do not select a drive root.");
            for (DirectoryInfo directory = new DirectoryInfo(normalized); directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("安装路径不能经过目录链接 / Directory links are not supported.");
            return normalized;
        }

        internal static string OwnedPath(string directory, string relative)
        {
            if (Array.IndexOf(OwnedFiles, relative) < 0) throw new InvalidDataException("Unexpected package file: " + relative);
            string root = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', '\\')));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escapes installation directory.");
            return path;
        }

        internal static string Option(string[] arguments, string name)
        {
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        internal static void SaveReport(string path, object report)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
        }

        internal static void EnsureNotRunning(string directory)
        {
            string root = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
            foreach (Process process in Process.GetProcesses())
            {
                if (process.Id == Process.GetCurrentProcess().Id) { process.Dispose(); continue; }
                bool running = false;
                try
                {
                    if (process.ProcessName != "PCL老蒋定制版 P1" && process.ProcessName != "Plain Craft Launcher 2") continue;
                    if (process.MainModule.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        running = true;
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
                if (running) throw new InvalidOperationException("请先关闭该安装目录中的启动器 / Close the launcher in this installation first.");
            }
        }
    }

    public sealed class InstallationReceipt
    {
        public string AppId { get; set; }
        public string Version { get; set; }
        public string[] Files { get; set; }
        public bool Registered { get; set; }
        public bool DesktopShortcut { get; set; }
    }
}
