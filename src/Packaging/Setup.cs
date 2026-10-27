using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PclThirdParty.Packaging
{
    internal static class PackageInstaller
    {
        internal static InstallationReceipt ReadReceipt(string directory)
        {
            string path = Path.Combine(directory, "installation.json");
            if (!File.Exists(path)) return null;
            var receipt = new JavaScriptSerializer().Deserialize<InstallationReceipt>(File.ReadAllText(path));
            if (receipt == null || receipt.AppId != PackageInfo.AppId) throw new InvalidDataException("该文件夹不属于本程序 / This directory belongs to another application.");
            foreach (string file in receipt.Files) PackageInfo.OwnedPath(directory, file);
            return receipt;
        }

        private static void DeleteWorkDirectory(string path, string parent, string token)
        {
            string expected = Path.GetFullPath(Path.Combine(parent, ".pcl-stage-" + token));
            if (!string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(token, "N", out GuidIgnored)) throw new InvalidOperationException("Unsafe cleanup directory.");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        private static Guid GuidIgnored;

        internal static void Install(string selectedDirectory, bool register, bool desktopShortcut, Action<int, string> progress)
        {
            string directory = PackageInfo.NormalizeDirectory(selectedDirectory);
            PackageInfo.EnsureNotRunning(directory);
            var previous = Directory.Exists(directory) ? ReadReceipt(directory) : null;
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length > 0 && previous == null)
                throw new InvalidOperationException("请选择空文件夹；不会覆盖现有 PCL 或其他文件 / Choose an empty directory; existing launcher files will not be overwritten.");
            string parent = Path.GetDirectoryName(directory);
            Directory.CreateDirectory(parent);
            string token = Guid.NewGuid().ToString("N");
            string staging = Path.Combine(parent, ".pcl-stage-" + token);
            Directory.CreateDirectory(staging);
            var committed = new List<string>();
            var backups = new Dictionary<string, string>();
            bool receiptWritten = false;
            try
            {
                Dictionary<string, string> hashes;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackageHashes"))
                using (var reader = new StreamReader(stream))
                    hashes = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackagePayload"))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count != PackageInfo.OwnedFiles.Length || hashes.Count != PackageInfo.OwnedFiles.Length)
                        throw new InvalidDataException("Invalid package inventory.");
                    long total = 0, copied = 0;
                    foreach (var entry in archive.Entries) total += entry.Length;
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in archive.Entries)
                    {
                        string path = PackageInfo.OwnedPath(staging, entry.FullName);
                        if (!seen.Add(entry.FullName) || !hashes.ContainsKey(entry.FullName)) throw new InvalidDataException("Duplicate or unknown package entry.");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (var input = entry.Open())
                        using (var output = File.Create(path))
                        {
                            var buffer = new byte[1024 * 1024];
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                output.Write(buffer, 0, read);
                                copied += read;
                                progress((int)(copied * 85 / Math.Max(1, total)), "copy");
                            }
                        }
                        using (var sha = SHA256.Create())
                        using (var file = File.OpenRead(path))
                            if (!string.Equals(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""), hashes[entry.FullName], StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("安装文件校验失败 / Package checksum failed: " + entry.FullName);
                    }
                }
                progress(90, "commit");
                Directory.CreateDirectory(directory);
                foreach (string relative in PackageInfo.OwnedFiles)
                {
                    string destination = PackageInfo.OwnedPath(directory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (File.Exists(destination))
                    {
                        string backup = Path.Combine(staging, "backup", relative.Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Move(destination, backup);
                        backups.Add(destination, backup);
                    }
                    File.Move(PackageInfo.OwnedPath(staging, relative), destination);
                    committed.Add(destination);
                }
                var receipt = new InstallationReceipt { AppId = PackageInfo.AppId, Version = "P1", Files = PackageInfo.OwnedFiles,
                    Registered = register || (previous != null && previous.Registered),
                    DesktopShortcut = desktopShortcut || (previous != null && previous.DesktopShortcut) };
                string receiptPath = Path.Combine(directory, "installation.json");
                File.WriteAllText(receiptPath + ".new", new JavaScriptSerializer().Serialize(receipt), new UTF8Encoding(false));
                if (File.Exists(receiptPath)) File.Replace(receiptPath + ".new", receiptPath, null);
                else File.Move(receiptPath + ".new", receiptPath);
                receiptWritten = true;
                if (receipt.Registered) Register(directory);
                if (desktopShortcut) UpdateDesktopShortcut(directory, false);
                progress(100, "done");
            }
            catch
            {
                if (!receiptWritten)
                {
                    foreach (string path in committed) if (File.Exists(path)) File.Delete(path);
                    foreach (var backup in backups) if (File.Exists(backup.Value)) File.Move(backup.Value, backup.Key);
                }
                throw;
            }
            finally { DeleteWorkDirectory(staging, parent, token); }
        }

        private static void UpdateDesktopShortcut(string directory, bool remove)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PCL老蒋定制版.lnk");
            if (remove && !File.Exists(path)) return;
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            object shortcut = null;
            try
            {
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                string target = Path.Combine(directory, "PCL老蒋定制版.exe");
                if (File.Exists(path))
                {
                    string existing = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                    if (!string.Equals(existing, target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (remove) return;
                        throw new IOException("同名桌面快捷方式指向其他程序，已保留 / Existing desktop shortcut points to another installation and was kept.");
                    }
                }
                if (remove) { File.Delete(path); return; }
                shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { directory });
                shortcut.GetType().InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { PackageInfo.Name });
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void Register(string directory)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PackageInfo.RegistryKey))
            {
                key.SetValue("DisplayName", PackageInfo.Name);
                key.SetValue("DisplayVersion", "P1");
                key.SetValue("Publisher", "Lao Jiang Custom Edition (independent PCL fork)");
                key.SetValue("InstallLocation", directory);
                key.SetValue("DisplayIcon", Path.Combine(directory, "PCL老蒋定制版.exe"));
                key.SetValue("UninstallString", PackageInfo.Quote(Path.Combine(directory, "Uninstall.exe")));
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 195000, RegistryValueKind.DWord);
            }
        }

        internal static void Uninstall(string selectedDirectory, Action<int, string> progress)
        {
            string directory = PackageInfo.NormalizeDirectory(selectedDirectory);
            var receipt = ReadReceipt(directory);
            if (receipt == null) throw new InvalidOperationException("找不到本程序安装记录 / Installation record not found.");
            PackageInfo.EnsureNotRunning(directory);
            if (receipt.DesktopShortcut) UpdateDesktopShortcut(directory, true);
            int count = 0;
            foreach (string relative in receipt.Files)
            {
                string path = PackageInfo.OwnedPath(directory, relative);
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try { if (File.Exists(path)) File.Delete(path); break; }
                    catch (IOException) { if (attempt == 9) throw; System.Threading.Thread.Sleep(100); }
                }
                progress(++count * 90 / receipt.Files.Length, "remove");
            }
            if (receipt.Registered)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PackageInfo.RegistryKey))
                    if (key != null && !string.Equals((string)key.GetValue("InstallLocation"), directory, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Registered installation path differs; preserved the registry entry.");
                Registry.CurrentUser.DeleteSubKey(PackageInfo.RegistryKey, false);
            }
            File.Delete(Path.Combine(directory, "installation.json"));
            foreach (string subdirectory in new[] { "app", "config" })
            {
                string path = Path.Combine(directory, subdirectory);
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0) Directory.Delete(path);
            }
            progress(100, "done");
        }
    }

    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] arguments)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool uninstall = false;
#if UNINSTALL
            uninstall = true;
            if (PackageInfo.Option(arguments, "--uninstall-root") == null)
            {
                string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                if (PackageInstaller.ReadReceipt(root) == null) return 1;
                string temporary = Path.Combine(root, "Data", "temp", "uninstall-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                string clone = Path.Combine(temporary, "Uninstall.exe");
                File.Copy(Application.ExecutablePath, clone);
                var forwarded = new List<string>(arguments);
                forwarded.Add("--uninstall-root"); forwarded.Add(root);
                var start = new ProcessStartInfo(clone, PackageInfo.Arguments(forwarded));
                start.UseShellExecute = false; start.CreateNoWindow = true; start.WorkingDirectory = root;
                using (var child = Process.Start(start)) { }
                return 0;
            }
#endif
            string report = PackageInfo.Option(arguments, "--report");
            string directory = uninstall ? PackageInfo.Option(arguments, "--uninstall-root") : PackageInfo.Option(arguments, "--install-dir");
            if (string.IsNullOrEmpty(directory)) directory = @"D:\Games\PCL-LaoJiang";
            bool register = Array.IndexOf(arguments, "--no-registration") < 0;
            if (Array.IndexOf(arguments, "--silent") >= 0)
            {
                try
                {
                    if (uninstall) PackageInstaller.Uninstall(directory, delegate { });
                    else PackageInstaller.Install(directory, register, Array.IndexOf(arguments, "--desktop-shortcut") >= 0, delegate { });
                    PackageInfo.SaveReport(report, new { status = "passed", mode = uninstall ? "uninstall" : "install", directory = directory });
                    return 0;
                }
                catch (Exception exception)
                {
                    PackageInfo.SaveReport(report, new { status = "failed", error = exception.ToString() });
                    return 1;
                }
            }
            using (var form = new SetupForm(uninstall, directory, register, Array.IndexOf(arguments, "--english") >= 0))
            {
                string preview = PackageInfo.Option(arguments, "--render-preview");
                if (!string.IsNullOrEmpty(preview))
                {
                    form.Show(); Application.DoEvents(); form.Refresh();
                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    { form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(preview); }
                    return 0;
                }
                Application.Run(form);
                return form.Completed ? 0 : 1;
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly bool uninstall;
        private bool english, busy;
        internal bool Completed;
        private Label title = new Label(), subtitle = new Label(), pathLabel = new Label(), note = new Label(), status = new Label();
        private TextBox directory = new TextBox();
        private Button browse = new Button(), install = new Button(), close = new Button(), open = new Button();
        private ComboBox language = new ComboBox();
        private CheckBox registration = new CheckBox(), desktop = new CheckBox();
        private ProgressBar progress = new ProgressBar();

        internal SetupForm(bool uninstall, string selectedDirectory, bool register, bool english)
        {
            this.uninstall = uninstall; this.english = english;
            Text = "PCL老蒋定制版 P1 安装程序"; ClientSize = new Size(660, 450);
            Font = new Font("Microsoft YaHei UI", 9F); BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon")) if (stream != null) Icon = new Icon(stream);
            title.SetBounds(28, 24, 450, 36); title.Font = new Font(Font.FontFamily, 20F, FontStyle.Bold); title.ForeColor = Color.FromArgb(30, 90, 170);
            language.SetBounds(505, 30, 125, 30); language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Items.AddRange(new object[] { "简体中文", "English" }); language.SelectedIndex = english ? 1 : 0;
            language.SelectedIndexChanged += delegate { this.english = language.SelectedIndex == 1; Translate(); };
            subtitle.SetBounds(30, 72, 600, 56); subtitle.ForeColor = Color.FromArgb(90, 90, 90);
            pathLabel.SetBounds(30, 142, 600, 24); directory.SetBounds(30, 170, 490, 29); directory.Text = selectedDirectory;
            directory.ReadOnly = uninstall;
            browse.SetBounds(532, 168, 98, 32); browse.Enabled = !uninstall;
            browse.Click += delegate { using (var dialog = new FolderBrowserDialog()) { dialog.SelectedPath = directory.Text; if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath; } };
            registration.SetBounds(30, 214, 600, 26); registration.Checked = register; registration.Visible = !uninstall;
            desktop.SetBounds(30, 240, 600, 26); desktop.Visible = !uninstall;
            note.SetBounds(30, 278, 600, 47); note.ForeColor = Color.FromArgb(90, 90, 90);
            progress.SetBounds(30, 336, 600, 17); status.SetBounds(30, 360, 600, 24);
            close.SetBounds(532, 403, 98, 32); close.Click += delegate { Close(); };
            install.SetBounds(400, 403, 120, 32); install.BackColor = Color.FromArgb(30, 100, 190); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat;
            install.Click += Run;
            open.SetBounds(250, 403, 138, 32); open.Visible = false;
            open.Click += delegate { var start = new ProcessStartInfo(Path.Combine(directory.Text, "PCL老蒋定制版.exe")); start.UseShellExecute = false; Process.Start(start); Close(); };
            Controls.AddRange(new Control[] { title, language, subtitle, pathLabel, directory, browse, registration, desktop, note, progress, status, install, open, close });
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (busy) args.Cancel = true; };
            Translate();
        }

        private string T(string chinese, string translated) { return english ? translated : chinese; }
        private void Translate()
        {
            title.Text = T(uninstall ? "卸载老蒋定制版" : "安装老蒋定制版 P1", uninstall ? "Uninstall Lao Jiang P1" : "Install Lao Jiang P1");
            subtitle.Text = T("P1 · 第三方定制版 · 支持中文和英语\n原作者：龙腾猫跃；社区基础：PCL Community", "Independent third-party development preview · Chinese and English\nOriginal author: 龙腾猫跃; community base: PCL Community");
            pathLabel.Text = T("安装目录", "Installation directory"); browse.Text = T("浏览…", "Browse…");
            registration.Text = T("添加到 Windows“已安装的应用”（当前用户，无需管理员权限）", "Add to Windows Installed Apps for the current user (no administrator required)");
            desktop.Text = T("创建桌面快捷方式（可选）", "Create a desktop shortcut (optional)");
            note.Text = T(uninstall ? "仅移除启动器程序；游戏、存档、账号配置和下载缓存会保留。" : "启动器、游戏数据和缓存默认保存在所选目录。\n不会导入其他启动器的存档或账号。", uninstall ? "Removes launcher files only. Games, saves, account configuration and caches are kept." : "Launcher, game data and caches stay in the selected directory.\nExisting launcher saves and accounts are not imported.");
            install.Text = T(uninstall ? "卸载" : "安装", uninstall ? "Uninstall" : "Install"); close.Text = T(Completed ? "完成" : "关闭", Completed ? "Finish" : "Close");
            open.Text = T("打开启动器", "Open launcher");
        }

        private async void Run(object sender, EventArgs args)
        {
            busy = true; install.Enabled = false; browse.Enabled = false; directory.Enabled = false; language.Enabled = false; registration.Enabled = false; desktop.Enabled = false; close.Enabled = false;
            string target = directory.Text; bool register = registration.Checked, shortcut = desktop.Checked;
            try
            {
                await Task.Run(delegate
                {
                    Action<int, string> changed = delegate(int percent, string stage)
                    {
                        BeginInvoke((Action)delegate { progress.Value = percent; status.Text = T(uninstall ? "正在卸载…" : "正在安装并校验文件…", uninstall ? "Uninstalling…" : "Installing and verifying files…"); });
                    };
                    if (uninstall) PackageInstaller.Uninstall(target, changed);
                    else PackageInstaller.Install(target, register, shortcut, changed);
                });
                Completed = true; progress.Value = 100;
                status.Text = T(uninstall ? "卸载完成，游戏数据已保留。" : "安装完成，可以直接双击 EXE 启动。", uninstall ? "Uninstalled. Game data was kept." : "Installed. Open the EXE to start the launcher.");
                open.Visible = !uninstall; install.Visible = false;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "PCL老蒋定制版 P1", MessageBoxButtons.OK, MessageBoxIcon.Error);
                install.Enabled = true; directory.Enabled = !uninstall; browse.Enabled = !uninstall;
            }
            finally { busy = false; close.Enabled = true; Translate(); }
        }
    }
}
