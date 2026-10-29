// Made by PapaGordon
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class ATM10
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ATM10",
            author = "MeFriendos",
            description = "WindowsGSM plugin for Minecraft: All the Mods 10 (ATM10 / NeoForge)",
            version = "0.1.12",
            url = "https://github.com/PapaGordon/WindowsGSM.ATM10",
            color = "#7AC943"
        };

        protected readonly ServerConfig _serverData;
        public string Error, Notice;

        public ATM10(ServerConfig serverData) { _serverData = serverData; }

        public virtual string StartPath => "startserver.bat";
        public string FullName = "Minecraft: All the Mods 10 (ATM10)";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public object QueryMethod = new UT3();

        public string Port = "25565";
        public string QueryPort = "25565";
        public string Defaultmap = "world";
        public string Maxplayers = "20";
        public string Additional = "";

        private const string VersionMarker = ".wgsm-atm10-version.txt";
        private const string ConfigDefaultsDirectory = ".wgsm-atm10-config-defaults";
        private const string PackModsManifest = "pack-mods.sha256";
        private static readonly string[] ConfigDirectories = { "config", "defaultconfigs" };
        private const string CurseForgeUrl = "https://www.curseforge.com/minecraft/modpacks/all-the-mods-10";

        public async void CreateServerCFG()
        {
            try
            {
                EnsureServerProperties();
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }

        public async Task<Process> Start()
        {
            try
            {
                using (AcquireUpdateLock(ServerPath.GetServersServerFiles(_serverData.ServerID))) { }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            string root = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string javaPath = FindJava21();
            if (string.IsNullOrWhiteSpace(javaPath))
                return null;

            string winArgs = FindWinArgsPath(root);
            if (string.IsNullOrWhiteSpace(winArgs))
            {
                Error = "NeoForge runtime not found. Run Install/Update first so the NeoForge installer can create libraries\\...\\win_args.txt.";
                return null;
            }

            string eulaPath = Path.Combine(root, "eula.txt");
            if (!File.Exists(eulaPath) || File.ReadAllText(eulaPath).IndexOf("eula=true", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Error = "Minecraft EULA is not accepted. Set eula=true in eula.txt first.";
                return null;
            }

            try
            {
                EnsureServerProperties();
            }
            catch (Exception e)
            {
                Error = "Unable to update server.properties: " + e.Message;
                return null;
            }

            string relativeWinArgs = MakeRelativePath(root, winArgs).Replace('/', '\\');
            var args = new StringBuilder();
            args.Append("@user_jvm_args.txt ");
            args.Append("@").Append(relativeWinArgs).Append(" ");
            args.Append("nogui ");
            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
                args.Append(_serverData.ServerParam).Append(" ");

            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = root,
                    FileName = javaPath,
                    Arguments = args.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            if (_serverData.EmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            try
            {
                p.Start();
                if (_serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p == null || p.HasExited)
                    return;

                try
                {
                    if (p.StartInfo.RedirectStandardInput)
                    {
                        p.StandardInput.WriteLine("stop");
                        p.StandardInput.Flush();
                    }
                    else
                    {
                        ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "stop");
                    }

                    if (!p.WaitForExit(30000))
                        p.Kill();
                }
                catch
                {
                    try
                    {
                        if (!p.HasExited)
                            p.Kill();
                    }
                    catch { }
                }
            });
        }

        public async Task<Process> Install()
        {
            var agreedPrompt = await UI.CreateYesNoPromptV1(
                "Minecraft EULA",
                "By continuing you confirm that you agree to the Minecraft EULA.\nhttps://www.minecraft.net/eula",
                "Agree",
                "Decline");

            if (!agreedPrompt)
            {
                Error = "Minecraft EULA was not accepted.";
                return null;
            }

            string javaPath = FindJava21();
            if (string.IsNullOrWhiteSpace(javaPath))
                return null;

            string packZip = FindNewestServerPackZip();
            if (string.IsNullOrWhiteSpace(packZip))
            {
                Error = "ATM10 server pack not found. Download ServerFiles-*.zip from CurseForge and place it in plugins\\ATM10.cs, the WindowsGSM root folder, or this server's serverfiles folder, then click Install again.";
                return null;
            }

            string root = ServerPath.GetServersServerFiles(_serverData.ServerID);

            try
            {
                await ExtractServerPack(packZip, root);
                File.WriteAllText(Path.Combine(root, "eula.txt"), "eula=true" + Environment.NewLine);
                EnsureServerProperties();
                WriteLocalVersion(root, packZip);
            }
            catch (Exception e)
            {
                Error = "ATM10 extraction failed: " + e.Message;
                return null;
            }

            return StartNeoForgeInstaller(root, javaPath);
        }

        public async Task<Process> Update()
        {
            Error = null;
            Notice = null;
            string javaPath = FindJava21();
            if (string.IsNullOrWhiteSpace(javaPath))
                return null;

            string packZip = FindNewestServerPackZip();
            if (string.IsNullOrWhiteSpace(packZip))
            {
                Error = "No ServerFiles-*.zip found. Download the new ATM10 server pack and place it in the WindowsGSM root folder, plugins\\ATM10.cs, or this server's serverfiles folder, then click Update again.";
                return null;
            }

            string root = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string localBuild = GetLocalBuild();
            string packBuild;
            try { packBuild = GetBuildFromServerPackZip(packZip); }
            catch (Exception e) { Error = "ATM10 server pack could not be read: " + e.Message; return null; }

            // WindowsGSM still allows the Update action when local and remote build
            // are identical. Do NOT unpack/reinstall the same 13+ GB modpack again.
            if (!string.IsNullOrWhiteSpace(localBuild) &&
                !string.IsNullOrWhiteSpace(packBuild) &&
                !string.Equals(localBuild, "unknown", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(localBuild, packBuild, StringComparison.OrdinalIgnoreCase))
            {
                Notice = "ATM10 " + localBuild + " is already up to date.";
                return null;
            }

            string extract = Path.Combine(Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar)),
                ".wgsm-atm10-extract-" + Guid.NewGuid().ToString("N"));
            Error = null;
            Notice = null;
            try
            {
                using (AcquireUpdateLock(root))
                {
                    Directory.CreateDirectory(extract);
                    await FileManagement.ExtractZip(packZip, extract);
                    ValidateTree(extract);
                    string packRoot = FindExtractedPackRoot(extract);
                    if (string.IsNullOrWhiteSpace(packRoot))
                        throw new IOException("startserver.bat was not found inside the selected server pack ZIP.");

                    UpdateResult result = await PrepareAndCommitUpdate(packRoot, root, packBuild,
                        stage => RunNeoForgeInstallerForUpdate(stage, javaPath));
                    Notice = "ATM10 updated successfully to " + packBuild + ". Backup: " + result.BackupPath +
                        (result.Conflicts == 0 ? "." : ". " + result.Conflicts +
                        " configuration conflict(s): existing settings kept. Review config-conflicts.txt in the backup folder.");
                }
            }
            catch (Exception e)
            {
                Error = "ATM10 update failed: " + e.Message;
            }
            finally
            {
                try { if (Directory.Exists(extract)) DeleteWritableDirectory(extract); }
                catch { /* Extraction leftovers are outside the live server directory. */ }
            }
            return null;
        }

        public bool IsInstallValid()
        {
            string root = ServerPath.GetServersServerFiles(_serverData.ServerID);
            return File.Exists(Path.Combine(root, "user_jvm_args.txt")) &&
                   !string.IsNullOrWhiteSpace(FindWinArgsPath(root));
        }

        public bool IsImportValid(string path)
        {
            bool valid = Directory.Exists(path) &&
                         File.Exists(Path.Combine(path, "user_jvm_args.txt")) &&
                         (!string.IsNullOrWhiteSpace(FindWinArgsPath(path)) || File.Exists(Path.Combine(path, StartPath)));
            if (!valid)
                Error = "Invalid ATM10 server folder. user_jvm_args.txt and an installed NeoForge runtime/startserver.bat are required.";
            return valid;
        }

        public string GetLocalBuild()
        {
            try
            {
                // BCC is what clients see. Do not hide an old config behind a newer marker.
                string identity = ServerPath.GetServersServerFiles(_serverData.ServerID, "config", "bcc-common.toml");
                if (File.Exists(identity))
                {
                    string advertised = ReadCompatibilityVersion(File.ReadAllText(identity));
                    return string.IsNullOrWhiteSpace(advertised) ? "unknown" : advertised;
                }
                string marker = ServerPath.GetServersServerFiles(_serverData.ServerID, VersionMarker);
                return File.Exists(marker) ? File.ReadAllText(marker).Trim() : "unknown";
            }
            catch { return "unknown"; }
        }

        public async Task<string> GetRemoteBuild()
        {
            try
            {
                // Update installs this local pack, not the version shown on a web page.
                string packZip = FindNewestServerPackZip();
                if (!string.IsNullOrWhiteSpace(packZip)) return GetBuildFromServerPackZip(packZip);
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsGSM-ATM10/0.1";
                    string html = await wc.DownloadStringTaskAsync(CurseForgeUrl);
                    Match match = Regex.Match(html, @"ServerFiles-([0-9][0-9A-Za-z._-]*)\.zip", RegexOptions.IgnoreCase);
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
            catch (Exception e)
            {
                Notice = "Could not check latest ATM10 version on CurseForge: " + e.Message;
            }

            return GetLocalBuild();
        }

        protected virtual string FindJava21()
        {
            // 1) Prefer JAVA_HOME when it points to Java 21+.
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string fromJavaHome = Path.Combine(javaHome.Trim().Trim('\"'), "bin", "java.exe");
                if (IsJava21OrNewer(fromJavaHome))
                    return fromJavaHome;
            }

            // 2) Search common Windows JDK locations. This also covers the
            //    MeFriendos installation C:\Program Files\Java\jdk-21.0.12.
            string[] commonRoots =
            {
                @"C:\Program Files\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Microsoft",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\BellSoft"
            };

            foreach (string root in commonRoots)
            {
                try
                {
                    if (!Directory.Exists(root))
                        continue;

                    foreach (string dir in Directory.GetDirectories(root, "*21*", SearchOption.TopDirectoryOnly)
                                                     .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d)))
                    {
                        string candidate = Path.Combine(dir, "bin", "java.exe");
                        if (IsJava21OrNewer(candidate))
                            return candidate;
                    }
                }
                catch { }
            }

            // 3) Fall back to WindowsGSM/PATH detection.
            string javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
            if (IsJava21OrNewer(javaPath))
                return javaPath;

            if (!string.IsNullOrWhiteSpace(javaPath) && File.Exists(javaPath))
            {
                int major = GetJavaMajorVersion(javaPath);
                Error = "Java " + (major > 0 ? major.ToString() : "unknown") +
                        " was found, but ATM10 / Minecraft 1.21.1 requires Java 21 or newer. " +
                        "Set JAVA_HOME to your Java 21 JDK or add its bin folder to PATH.";
            }
            else
            {
                Error = "Java 21 was not found. Install a 64-bit Java 21 JDK, set JAVA_HOME, " +
                        "or add its bin folder to PATH. Common locations such as C:\\Program Files\\Java\\jdk-21* are checked automatically.";
            }

            return null;
        }

        private bool IsJava21OrNewer(string javaPath)
        {
            if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                return false;

            int major = GetJavaMajorVersion(javaPath);
            return major >= 21;
        }

        private int GetJavaMajorVersion(string javaPath)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string text = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);

                Match m = Regex.Match(text, "version\\s+\\\"(?<major>\\d+)(?:\\.(?<minor>\\d+))?");
                if (!m.Success)
                    return 0;

                int first = int.Parse(m.Groups["major"].Value);
                if (first == 1 && m.Groups["minor"].Success)
                    return int.Parse(m.Groups["minor"].Value);
                return first;
            }
            catch
            {
                return 0;
            }
        }

        private string GetBuildFromServerPackZip(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
                return string.Empty;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var identities = archive.Entries.Where(e =>
                {
                    string path = e.FullName.Replace('\\', '/');
                    return path.Equals("config/bcc-common.toml", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith("/config/bcc-common.toml", StringComparison.OrdinalIgnoreCase);
                }).ToArray();
                if (identities.Length > 1) throw new IOException("The server pack contains multiple BCC identity files.");
                if (identities.Length == 1)
                {
                    if (identities[0].Length > 65536) throw new IOException("The server pack BCC identity file is too large.");
                    using (var reader = new StreamReader(identities[0].Open()))
                    {
                        string version = ReadCompatibilityVersion(reader.ReadToEnd());
                        if (string.IsNullOrWhiteSpace(version)) throw new IOException("The server pack BCC version is missing or invalid.");
                        return version;
                    }
                }
            }

            Match match = Regex.Match(
                Path.GetFileName(zipPath),
                @"^ServerFiles-(.+)\.zip$",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private async Task<bool> RunNeoForgeInstallerForUpdate(string root, string javaPath)
        {
            string installer = FindNeoForgeInstaller(root);

            // Some pack updates do not ship a new installer. If a valid NeoForge
            // runtime already exists, there is nothing to do.
            if (string.IsNullOrWhiteSpace(installer))
            {
                if (!string.IsNullOrWhiteSpace(FindWinArgsPath(root)))
                    return true;

                Error = "NeoForge installer JAR was not found and no installed NeoForge runtime is available.";
                return false;
            }

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = root,
                    FileName = javaPath,
                    Arguments = "-jar \"" + installer + "\" --installServer",
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,

                    // IMPORTANT: do not redirect these streams. Update() waits for
                    // the process itself and then returns null to WindowsGSM.
                    // This avoids the synchronous/asynchronous stream conflict and
                    // the update screen getting stuck.
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                p.Start();
                await Task.Run(() => p.WaitForExit());

                if (p.ExitCode != 0)
                {
                    Error = "NeoForge installer exited with code " + p.ExitCode + ".";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(FindWinArgsPath(root)))
                {
                    Error = "NeoForge installer finished, but win_args.txt was not created.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Error = "Could not run NeoForge installer during update: " + e.Message;
                return false;
            }
            finally
            {
                p.Dispose();
            }
        }

        private Process StartNeoForgeInstaller(string root, string javaPath)
        {
            if (!string.IsNullOrWhiteSpace(FindWinArgsPath(root)))
            {
                string installerExisting = FindNeoForgeInstaller(root);
                if (string.IsNullOrWhiteSpace(installerExisting))
                    return null;
            }

            string installer = FindNeoForgeInstaller(root);
            if (string.IsNullOrWhiteSpace(installer))
            {
                Error = "NeoForge installer JAR was not found in the ATM10 server pack.";
                return null;
            }

            // IMPORTANT: Install()/Update() return this Process to WindowsGSM.
            // WindowsGSM v1.25.1 reads Installer.StandardOutput synchronously in
            // Button_Install_Click. Therefore the plugin MUST NOT call
            // BeginOutputReadLine()/BeginErrorReadLine() on this process, otherwise
            // .NET throws: "Cannot mix synchronous and asynchronous operation on process stream.".
            //
            // Redirect stdout for the WindowsGSM install log. stderr is intentionally
            // left unredirected so there is no unread redirected stream that could
            // block a verbose Java installer.
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = root,
                    FileName = javaPath,
                    Arguments = "-jar \"" + installer + "\" --installServer",
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            try
            {
                p.Start();
                return p;
            }
            catch (Exception e)
            {
                Error = "Could not start NeoForge installer: " + e.Message;
                return null;
            }
        }

        private string FindNeoForgeInstaller(string root)
        {
            if (!Directory.Exists(root))
                return null;

            return Directory.GetFiles(root, "neoforge-*-installer.jar", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .FirstOrDefault();
        }

        protected virtual string FindWinArgsPath(string root)
        {
            try
            {
                string neoForgeRoot = Path.Combine(root, "libraries", "net", "neoforged", "neoforge");
                if (!Directory.Exists(neoForgeRoot))
                    return null;

                var candidates = Directory.GetDirectories(neoForgeRoot)
                    .Select(d => new
                    {
                        Directory = d,
                        Version = ParseVersionSafe(Path.GetFileName(d))
                    })
                    .OrderByDescending(x => x.Version)
                    .ThenByDescending(x => Directory.GetLastWriteTimeUtc(x.Directory));

                foreach (var candidate in candidates)
                {
                    string winArgs = Path.Combine(candidate.Directory, "win_args.txt");
                    if (File.Exists(winArgs))
                        return winArgs;
                }
            }
            catch { }

            return null;
        }

        private Version ParseVersionSafe(string text)
        {
            Version v;
            if (Version.TryParse(text, out v))
                return v;
            return new Version(0, 0);
        }

        private string[] FindServerPackZips()
        {
            var roots = new List<string>();

            // The WindowsGSM process can have its AppDomain base inside \bin,
            // so derive the real WindowsGSM root from the known serverfiles path.
            string serverFiles = ServerPath.GetServersServerFiles(_serverData.ServerID);
            roots.Add(serverFiles);

            try
            {
                DirectoryInfo serverFilesDir = new DirectoryInfo(serverFiles);
                // ...\WindowsGSM\servers\<id>\serverfiles -> ...\WindowsGSM
                DirectoryInfo wgsmRoot = serverFilesDir.Parent != null &&
                                         serverFilesDir.Parent.Parent != null
                    ? serverFilesDir.Parent.Parent.Parent
                    : null;

                if (wgsmRoot != null)
                {
                    roots.Add(wgsmRoot.FullName);
                    roots.Add(Path.Combine(wgsmRoot.FullName, "plugins", "ATM10.cs"));
                }
            }
            catch { }

            // Keep the previous locations as fallbacks for unusual WindowsGSM layouts.
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                roots.Add(baseDir);
                roots.Add(Path.Combine(baseDir, "plugins", "ATM10.cs"));

                DirectoryInfo baseInfo = new DirectoryInfo(baseDir);
                if (baseInfo.Parent != null)
                {
                    roots.Add(baseInfo.Parent.FullName);
                    roots.Add(Path.Combine(baseInfo.Parent.FullName, "plugins", "ATM10.cs"));
                }
            }
            catch { }

            try
            {
                roots.Add(Directory.GetCurrentDirectory());
            }
            catch { }

            var files = new List<string>();
            foreach (string root in roots
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(root))
                        continue;

                    files.AddRange(Directory.GetFiles(root, "ServerFiles-*", SearchOption.TopDirectoryOnly)
                        .Where(f => string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase)));
                }
                catch { }
            }

            return files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string FindNewestServerPackZip()
        {
            var candidates = new List<KeyValuePair<string, Version>>();
            foreach (string file in FindServerPackZips())
            {
                try
                {
                    string build = GetBuildFromServerPackZip(file);
                    Match numeric = Regex.Match(build ?? "", @"^\d+(?:\.\d+){1,3}");
                    Version version;
                    if (!numeric.Success || !Version.TryParse(numeric.Value, out version)) version = new Version(0, 0);
                    candidates.Add(new KeyValuePair<string, Version>(file, version));
                }
                catch (IOException) { /* An unreadable older archive is not an update candidate. */ }
                catch (UnauthorizedAccessException) { }
            }
            // Keeping an old ZIP for ownership migration must not select a downgrade.
            return candidates.OrderByDescending(c => c.Value).ThenByDescending(c => File.GetLastWriteTimeUtc(c.Key))
                .Select(c => c.Key).FirstOrDefault();
        }

        private async Task ExtractServerPack(string zipPath, string root)
        {
            string temp = Path.Combine(root, "_wgsm_atm10_extract");
            if (Directory.Exists(temp))
                DeleteWritableDirectory(temp);
            Directory.CreateDirectory(temp);

            try
            {
                await FileManagement.ExtractZip(zipPath, temp);
                ValidateTree(temp);
                string packRoot = FindExtractedPackRoot(temp);
                if (string.IsNullOrWhiteSpace(packRoot))
                    throw new IOException("startserver.bat was not found inside the selected server pack ZIP.");

                CopyDirectoryContents(packRoot, root, true, true);
                SaveConfigDefaults(packRoot, root, GetBuildFromServerPackZip(zipPath));
            }
            finally
            {
                try { if (Directory.Exists(temp)) DeleteWritableDirectory(temp); }
                catch { }
            }
        }

        private string FindExtractedPackRoot(string temp)
        {
            string direct = Path.Combine(temp, StartPath);
            if (File.Exists(direct))
                return temp;

            string found = Directory.GetFiles(temp, StartPath, SearchOption.AllDirectories)
                .OrderBy(f => f.Length)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(found) ? null : Path.GetDirectoryName(found);
        }

        private sealed class UpdateResult
        {
            public string BackupPath;
            public int Conflicts;
        }

        // Prepare the complete update next to serverfiles on the same volume.
        // No existing server file is changed until preparation and NeoForge succeed.
        private async Task<UpdateResult> PrepareAndCommitUpdate(string packRoot, string root,
            string packBuild, Func<string, Task<bool>> installRuntime)
        {
            ValidateTree(root);
            ValidateTree(packRoot);
            if (Directory.Exists(Path.Combine(packRoot, ConfigDefaultsDirectory)) ||
                File.Exists(Path.Combine(packRoot, ConfigDefaultsDirectory)))
                throw new IOException("The server pack contains a reserved plugin state path.");

            string parent = Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar));
            string backup = Path.Combine(parent, "atm10-update-backups",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(backup, "prepared-serverfiles");
            var conflicts = new List<string>();
            try
            {
                CopyDirectoryContents(root, stage, true);
                CopyPackForUpdate(packRoot, stage);
                MergePackMods(packRoot, root, stage);
                bool knownDefaults = HasConfigDefaults(root);
                foreach (string name in ConfigDirectories)
                {
                    string incoming = Path.Combine(packRoot, name);
                    if (Directory.Exists(incoming))
                        CopyDirectoryContents(incoming, Path.Combine(backup, "incoming-config", name), true);
                    MergeConfigDirectory(incoming, Path.Combine(stage, name),
                        Path.Combine(root, ConfigDefaultsDirectory, name), knownDefaults, name, conflicts);
                }

                SynchronizePackIdentity(packRoot, stage);

                EnsureServerProperties(stage);
                if (!await installRuntime(stage))
                    throw new IOException(string.IsNullOrWhiteSpace(Error) ? "NeoForge installation failed." : Error);

                string existingJvmArgs = Path.Combine(root, "user_jvm_args.txt");
                if (File.Exists(existingJvmArgs))
                    CopyWritableFile(existingJvmArgs, Path.Combine(stage, "user_jvm_args.txt"), true);

                SaveConfigDefaults(packRoot, stage, packBuild);
                File.WriteAllText(Path.Combine(stage, VersionMarker), packBuild);
                File.WriteAllLines(Path.Combine(backup, "config-conflicts.txt"), new[]
                {
                    "Existing settings were kept for the files listed below.",
                    "New server-pack defaults are in incoming-config. Compare and apply any required changes before starting the server.",
                    knownDefaults ? "Compared against the previous server-pack defaults." :
                        "No previous defaults were available. Existing configuration files were kept conservatively.",
                    ""
                }.Concat(conflicts).ToArray());
            }
            catch (Exception e)
            {
                try { if (Directory.Exists(stage)) DeleteWritableDirectory(stage); } catch { }
                throw new IOException("Preparation failed; existing serverfiles were not changed. " + e.Message, e);
            }

            PublishPreparedUpdate(root, stage, backup);
            return new UpdateResult { BackupPath = backup, Conflicts = conflicts.Count };
        }

        protected virtual string RecoveryMarker(string root)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)),
                ".wgsm-atm10-update-recovery.txt");
        }

        protected virtual FileStream AcquireUpdateLock(string root)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar));
            FileStream updateLock;
            try
            {
                updateLock = new FileStream(Path.Combine(parent, ".wgsm-atm10-update.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException e)
            {
                throw new IOException("Another ATM10 update is active or the update lock cannot be opened: " + e.Message, e);
            }
            if (File.Exists(RecoveryMarker(root)))
            {
                updateLock.Dispose();
                throw new IOException("An interrupted ATM10 update needs recovery. Read " + RecoveryMarker(root) +
                    " before starting or updating this server.");
            }
            return updateLock;
        }

        protected void PublishPreparedUpdate(string root, string stage, string backup)
        {
            string previous = Path.Combine(backup, "serverfiles");
            string marker = RecoveryMarker(root);
            // Flush recovery instructions before either rename. Keep this marker if
            // restoration fails so Start() cannot launch a partially recovered server.
            using (var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] text = Encoding.UTF8.GetBytes("ATM10 update recovery\r\nLive: " + root +
                    "\r\nPrevious server: " + previous + "\r\nPrepared server: " + stage +
                    "\r\nIf Live is missing, restore Previous server to Live. Keep the backup until the server is verified.\r\n" +
                    "After checking/restoring the server, remove this recovery file to allow startup.\r\n");
                stream.Write(text, 0, text.Length);
                stream.Flush(true);
            }
            try
            {
                Directory.Move(root, previous);
                try
                {
                    Directory.Move(stage, root);
                }
                catch (Exception publishError)
                {
                    try { Directory.Move(previous, root); }
                    catch (Exception restoreError)
                    {
                        throw new IOException("Could not activate the update or restore the previous server. " +
                            "The previous server is retained at " + previous + ". Recovery instructions: " + marker +
                            ". Activate: " + publishError.Message + "; restore: " + restoreError.Message, restoreError);
                    }
                    throw new IOException("Could not activate the update; the previous server was restored. " +
                        publishError.Message, publishError);
                }
            }
            catch
            {
                if (!Directory.Exists(previous) && Directory.Exists(root))
                    File.Delete(marker);
                throw;
            }
            File.Delete(marker);
        }

        private bool HasConfigDefaults(string root)
        {
            string savedVersion = Path.Combine(root, ConfigDefaultsDirectory, VersionMarker);
            string installedVersion = Path.Combine(root, VersionMarker);
            return File.Exists(savedVersion) && File.Exists(installedVersion) &&
                !string.IsNullOrWhiteSpace(File.ReadAllText(savedVersion)) &&
                string.Equals(File.ReadAllText(savedVersion), File.ReadAllText(installedVersion), StringComparison.Ordinal);
        }

        private static readonly string[] PackIdentityKeys = { "modpackProjectID", "modpackName", "modpackVersion", "useMetadata" };

        private int CommentOffset(string value)
        {
            char quote = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (quote == '"' && c == '\\') { i++; continue; }
                if (quote != '\0') { if (c == quote) quote = '\0'; }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == '#') return i;
            }
            return value.Length;
        }

        private Dictionary<string, string> ReadIdentitySettings(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            string section = string.Empty;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim().TrimStart('\uFEFF');
                var header = Regex.Match(line, @"^\[([^\]]+)\]\s*(?:#.*)?$");
                if (header.Success) { section = header.Groups[1].Value.Trim(); continue; }
                if (section != "general") continue;
                var setting = Regex.Match(line, @"^([A-Za-z][A-Za-z0-9_]*)\s*=\s*(.*)$");
                if (!setting.Success || !PackIdentityKeys.Contains(setting.Groups[1].Value)) continue;
                string key = setting.Groups[1].Value, value = setting.Groups[2].Value;
                if (values.ContainsKey(key)) throw new IOException("Duplicate BCC identity setting: " + key);
                values.Add(key, value.Substring(0, CommentOffset(value)).Trim());
            }
            return values;
        }

        private string ReadCompatibilityVersion(string text)
        {
            string value;
            if (!ReadIdentitySettings(text).TryGetValue("modpackVersion", out value)) return null;
            var match = Regex.Match(value, "^([\"'])([0-9][0-9A-Za-z._+-]*)\\1$");
            return match.Success ? match.Groups[2].Value : null;
        }

        private void SynchronizePackIdentity(string source, string destination)
        {
            string incoming = Path.Combine(source, "config", "bcc-common.toml");
            if (!File.Exists(incoming)) return;
            string text = File.ReadAllText(incoming);
            if (string.IsNullOrWhiteSpace(ReadCompatibilityVersion(text)))
                throw new IOException("The server pack BCC version is missing or invalid.");
            var values = ReadIdentitySettings(text);
            string target = Path.Combine(destination, "config", "bcc-common.toml");
            if (Directory.Exists(target)) throw new IOException("A directory blocks the BCC identity file.");
            var lines = File.Exists(target) ? File.ReadAllLines(target).ToList() : new List<string>();
            foreach (var setting in values)
            {
                int general = -1, end = lines.Count, found = -1;
                string section = string.Empty;
                for (int i = 0; i < lines.Count; i++)
                {
                    var header = Regex.Match(lines[i].Trim().TrimStart('\uFEFF'), @"^\[([^\]]+)\]\s*(?:#.*)?$");
                    if (header.Success)
                    {
                        if (section == "general") end = Math.Min(end, i);
                        section = header.Groups[1].Value.Trim();
                        if (section == "general")
                        {
                            if (general >= 0) throw new IOException("Duplicate BCC general section.");
                            general = i;
                        }
                    }
                    if (section != "general" || !Regex.IsMatch(lines[i], @"^\s*" + setting.Key + @"\s*=")) continue;
                    if (found >= 0) throw new IOException("Duplicate BCC identity setting: " + setting.Key);
                    found = i;
                }
                if (found >= 0)
                {
                    string line = lines[found];
                    int equals = line.IndexOf('='), comment = CommentOffset(line.Substring(equals + 1));
                    string suffix = line.Substring(equals + 1 + comment);
                    lines[found] = line.Substring(0, equals + 1) + " " + setting.Value + (suffix.Length == 0 ? "" : " " + suffix);
                }
                else
                {
                    if (general < 0) { lines.Add("[general]"); end = lines.Count; }
                    lines.Insert(end, setting.Key + " = " + setting.Value);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllLines(target, lines);
            if (ReadCompatibilityVersion(File.ReadAllText(target)) != ReadCompatibilityVersion(text))
                throw new IOException("The prepared BCC identity does not match the selected server pack.");
        }

        private void SaveConfigDefaults(string packRoot, string destination, string version)
        {
            string defaults = Path.Combine(destination, ConfigDefaultsDirectory);
            if (Directory.Exists(defaults)) DeleteWritableDirectory(defaults);
            Directory.CreateDirectory(defaults);
            foreach (string name in ConfigDirectories)
            {
                string source = Path.Combine(packRoot, name);
                if (Directory.Exists(source)) CopyDirectoryContents(source, Path.Combine(defaults, name), true);
            }
            File.WriteAllText(Path.Combine(defaults, VersionMarker), version);
            WritePackModInventory(Path.Combine(defaults, PackModsManifest), version, InventoryDirectory(Path.Combine(packRoot, "mods")));
        }

        // File-level three-way comparison: old pack default, local file, new pack
        // default. Never guess how to merge TOML/JSON or overwrite a local change.
        private void MergeConfigDirectory(string incoming, string current, string baseline,
            bool knownDefaults, string name, List<string> conflicts)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(incoming))
                foreach (string file in Directory.GetFiles(incoming, "*", SearchOption.AllDirectories))
                    paths.Add(MakeRelativePath(incoming, file));
            if (knownDefaults && Directory.Exists(baseline))
                foreach (string file in Directory.GetFiles(baseline, "*", SearchOption.AllDirectories))
                    paths.Add(MakeRelativePath(baseline, file));

            foreach (string relative in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string next = Path.Combine(incoming, relative);
                string local = Path.Combine(current, relative);
                string old = Path.Combine(baseline, relative);
                if (Directory.Exists(local) || HasFileInParentPath(current, relative))
                {
                    conflicts.Add(name + "/" + relative.Replace('\\', '/') + " (file/directory conflict; existing path kept)");
                    continue;
                }
                bool unchanged = knownDefaults ? SameFileContents(local, old) : !File.Exists(local);
                if (unchanged)
                {
                    if (File.Exists(next)) CopyWritableFile(next, local, true);
                    else if (File.Exists(local)) DeleteWritableFile(local);
                }
                else if (!SameFileContents(local, next) &&
                    (!knownDefaults || !SameFileContents(old, next)))
                {
                    conflicts.Add(name + "/" + relative.Replace('\\', '/') +
                        (File.Exists(local) ? " (existing file kept)" : " (local deletion kept)"));
                }
            }
        }

        private bool HasFileInParentPath(string root, string relative)
        {
            if (File.Exists(root)) return true;
            string parent = Path.GetDirectoryName(relative);
            while (!string.IsNullOrEmpty(parent))
            {
                if (File.Exists(Path.Combine(root, parent))) return true;
                parent = Path.GetDirectoryName(parent);
            }
            return false;
        }

        private bool SameFileContents(string first, string second)
        {
            bool a = File.Exists(first), b = File.Exists(second);
            if (!a || !b) return a == b;
            if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
            using (var left = File.OpenRead(first))
            using (var right = File.OpenRead(second))
            {
                var x = new byte[8192];
                var y = new byte[8192];
                int count;
                while ((count = left.Read(x, 0, x.Length)) > 0)
                {
                    int offset = 0, read;
                    while (offset < count && (read = right.Read(y, offset, count - offset)) > 0) offset += read;
                    if (offset != count) return false;
                    for (int i = 0; i < count; i++) if (x[i] != y[i]) return false;
                }
                return right.ReadByte() == -1;
            }
        }

        private string ModFileHash(Stream stream)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private string ModFileHash(string file)
        {
            using (var stream = File.OpenRead(file)) return ModFileHash(stream);
        }

        private string SafeModPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOfAny(new[] { '\t', '\r', '\n', ':' }) >= 0 ||
                relative.Replace('\\', '/').Split('/').Any(p => p == ".." || p == "." || p.Length == 0))
                throw new IOException("Invalid path in the pack mod inventory.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Mod path leaves the mods directory.");
            return result;
        }

        private Dictionary<string, string> InventoryDirectory(string mods)
        {
            var inventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(mods)) return inventory;
            foreach (string file in Directory.GetFiles(mods, "*", SearchOption.AllDirectories))
            {
                string relative = MakeRelativePath(mods, file).Replace('\\', '/');
                SafeModPath(mods, relative);
                inventory.Add(relative, ModFileHash(file));
            }
            return inventory;
        }

        private void WritePackModInventory(string path, string version, Dictionary<string, string> inventory)
        {
            if (version.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0) throw new IOException("Invalid pack inventory version.");
            File.WriteAllLines(path, new[] { "version\t" + version }.Concat(inventory.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Value + "\t" + p.Key)));
        }

        private Dictionary<string, string> ReadPackModInventory(string root)
        {
            string path = Path.Combine(root, ConfigDefaultsDirectory, PackModsManifest);
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("Pack mod inventory is too large.");
                var inventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = File.ReadAllLines(path);
                if (lines.Length == 0 || !lines[0].StartsWith("version\t", StringComparison.Ordinal) || lines[0].Length <= "version\t".Length)
                    throw new IOException("Invalid pack mod inventory header. Restore the inventory backup before updating.");
                foreach (string line in lines.Skip(1))
                {
                    var pair = line.Split(new[] { '\t' }, 2);
                    if (pair.Length != 2 || !Regex.IsMatch(pair[0], "^[a-f0-9]{64}$")) throw new IOException("Invalid pack mod inventory entry.");
                    SafeModPath(Path.Combine(root, "mods"), pair[1]);
                    inventory.Add(pair[1], pair[0]);
                }
                return inventory;
            }
            string mods = Path.Combine(root, "mods");
            if (!Directory.Exists(mods) || Directory.GetFiles(mods, "*", SearchOption.AllDirectories).Length == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string versionFile = Path.Combine(root, VersionMarker);
            string installed = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : GetLocalBuild();
            foreach (string zip in FindServerPackZips())
            {
                if (GetBuildFromServerPackZip(zip) != installed) continue;
                using (var archive = ZipFile.OpenRead(zip))
                {
                    var launcher = archive.Entries.Where(e => e.FullName.Replace('\\', '/').Split('/').Last().Equals("startserver.bat", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(e => e.FullName.Length).FirstOrDefault();
                    if (launcher == null) continue;
                    string prefix = launcher.FullName.Replace('\\', '/');
                    prefix = prefix.Substring(0, prefix.Length - "startserver.bat".Length) + "mods/";
                    var inventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in archive.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.EndsWith("/", StringComparison.Ordinal)) continue;
                        string relative = name.Substring(prefix.Length);
                        SafeModPath(mods, relative);
                        using (var stream = entry.Open()) inventory.Add(relative, ModFileHash(stream));
                    }
                    if (inventory.Count > 0) return inventory;
                }
            }
            throw new IOException("Cannot safely distinguish old pack mods from additional mods. Place the previous original ServerFiles-" + installed +
                ".zip beside the new server ZIP and retry. Existing mods were not changed.");
        }

        private void MergePackMods(string source, string previous, string stage)
        {
            string incomingMods = Path.Combine(source, "mods"), liveMods = Path.Combine(previous, "mods"), preparedMods = Path.Combine(stage, "mods");
            if (!Directory.Exists(incomingMods))
            {
                if (Directory.Exists(liveMods) && Directory.GetFiles(liveMods, "*", SearchOption.AllDirectories).Length > 0)
                    throw new IOException("The selected server pack has no mods directory. Use the complete official ServerFiles ZIP.");
                return;
            }
            var owned = ReadPackModInventory(previous);
            var incoming = InventoryDirectory(incomingMods);
            foreach (var old in owned)
            {
                if (incoming.ContainsKey(old.Key)) continue;
                string file = SafeModPath(preparedMods, old.Key);
                if (!File.Exists(file)) continue;
                if (ModFileHash(file) != old.Value) throw new IOException("A pack mod was changed locally: " + old.Key + ". Resolve this conflict before updating.");
                DeleteWritableFile(file);
            }
            foreach (var next in incoming)
            {
                string file = SafeModPath(preparedMods, next.Key);
                if (Directory.Exists(file)) throw new IOException("A directory blocks pack mod " + next.Key);
                if (File.Exists(file))
                {
                    string actual = ModFileHash(file), oldHash;
                    if (actual == next.Value) continue;
                    if (!owned.TryGetValue(next.Key, out oldHash) || actual != oldHash)
                        throw new IOException("An additional or locally changed mod conflicts with a new pack file: " + next.Key + ". Existing server files were not changed.");
                }
                CopyWritableFile(SafeModPath(incomingMods, next.Key), file, true);
            }
            var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string jar in Directory.Exists(preparedMods) ? Directory.GetFiles(preparedMods, "*.jar", SearchOption.AllDirectories) : new string[0])
            {
                foreach (string id in ReadModIds(jar))
                {
                    string other;
                    if (ids.TryGetValue(id, out other) && !string.Equals(other, jar, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Duplicate mod ID " + id + " in " + Path.GetFileName(other) + " and " + Path.GetFileName(jar) + ". Resolve the additional mod conflict before updating.");
                    ids[id] = jar;
                }
            }
        }

        private string[] ReadModIds(string jar)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(jar))
                {
                    var entry = archive.GetEntry("META-INF/neoforge.mods.toml") ?? archive.GetEntry("META-INF/mods.toml");
                    if (entry == null || entry.Length > 1024 * 1024) return new string[0];
                    var ids = new List<string>();
                    bool modSection = false;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string trimmed = line.Trim();
                            if (trimmed.StartsWith("[", StringComparison.Ordinal))
                            {
                                modSection = Regex.IsMatch(trimmed, @"^\[\[mods\]\]\s*(?:#.*)?$");
                                continue;
                            }
                            if (!modSection) continue;
                            var match = Regex.Match(line, "^\\s*modId\\s*=\\s*[\"']([a-z][a-z0-9_]*)[\"']");
                            if (match.Success) ids.Add(match.Groups[1].Value);
                        }
                    }
                    return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                }
            }
            catch (InvalidDataException) { return new string[0]; }
        }

        private void CopyPackForUpdate(string source, string destination)
        {
            var preserveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "eula.txt", "server.properties", "ops.json", "whitelist.json",
                "banned-ips.json", "banned-players.json", VersionMarker
            };
            var preserveDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "world", "world_nether", "world_the_end", "logs", "crash-reports", "backups",
                "config", "defaultconfigs", "mods", ConfigDefaultsDirectory
            };
            // Also protect a world whose name differs from Minecraft's default.
            string properties = Path.Combine(destination, "server.properties");
            if (File.Exists(properties))
            {
                string level = File.ReadAllLines(properties).FirstOrDefault(l => l.StartsWith("level-name=", StringComparison.OrdinalIgnoreCase));
                if (level != null)
                {
                    string value = level.Substring("level-name=".Length).Trim();
                    string top = value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(top)) preserveDirectories.Add(top);
                }
            }
            foreach (string oldInstaller in Directory.GetFiles(destination, "neoforge-*-installer.jar", SearchOption.TopDirectoryOnly))
                DeleteWritableFile(oldInstaller);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);
                if (preserveFiles.Contains(name) || preserveDirectories.Contains(name)) continue;
                // Keep the whole JVM args file, not only -Xms and -Xmx.
                if (string.Equals(name, "user_jvm_args.txt", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(destination, name))) continue;
                CopyWritableFile(file, Path.Combine(destination, name), true);
            }
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(dir);
                if (preserveDirectories.Contains(name) || preserveFiles.Contains(name)) continue;
                string dest = Path.Combine(destination, name);
                if (Directory.Exists(dest)) DeleteWritableDirectory(dest);
                else if (File.Exists(dest)) DeleteWritableFile(dest);
                CopyDirectoryContents(dir, dest, true);
            }
        }

        protected void ValidateTree(string root)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked files or directories need a manual update: " + root);
            if (!Directory.Exists(root)) return;
            foreach (string entry in Directory.GetFileSystemEntries(root)) ValidateTree(entry);
        }

        private void CopyWritableFile(string source, string destination, bool overwrite)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(destination)) ClearReadOnly(destination);
            File.Copy(source, destination, overwrite);
            ClearReadOnly(destination);
        }

        private void ClearReadOnly(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        private void DeleteWritableFile(string path)
        {
            ClearReadOnly(path);
            File.Delete(path);
        }

        protected void DeleteWritableDirectory(string path)
        {
            ValidateTree(path);
            foreach (string file in Directory.GetFiles(path)) DeleteWritableFile(file);
            foreach (string dir in Directory.GetDirectories(path)) DeleteWritableDirectory(dir);
            ClearReadOnly(path);
            Directory.Delete(path);
        }

        protected void CopyDirectoryContents(string source, string destination, bool overwrite,
            bool keepJvmArgs = false)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (keepJvmArgs &&
                    string.Equals(Path.GetFileName(file), "user_jvm_args.txt", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(target)) continue;
                CopyWritableFile(file, target, overwrite);
            }
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
                CopyDirectoryContents(dir, Path.Combine(destination, Path.GetFileName(dir)), overwrite);
        }

        protected void EnsureServerProperties()
        {
            EnsureServerProperties(ServerPath.GetServersServerFiles(_serverData.ServerID));
        }

        protected void EnsureServerProperties(string root)
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "server.properties");

            var lines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();

            // Preserve a custom MOTD. WindowsGSM's ServerName is not the Minecraft
            // MOTD and must not overwrite server.properties on every start/update.
            bool hasMotd = lines.Any(line =>
                line.StartsWith("motd=", StringComparison.OrdinalIgnoreCase));

            if (!hasMotd)
                SetProperty(lines, "motd", FullName);

            SetProperty(lines, "server-port", _serverData.ServerPort);
            SetProperty(lines, "enable-query", "true");
            SetProperty(lines, "query.port", _serverData.ServerQueryPort);
            SetProperty(lines, "max-players", _serverData.ServerMaxPlayer);
            SetProperty(lines, "allow-flight", "true");

            File.WriteAllLines(path, lines.ToArray());
        }

        private void SetProperty(List<string> lines, string key, string value)
        {
            string prefix = key + "=";
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = prefix + EscapePropertyValue(value);
                    return;
                }
            }
            lines.Add(prefix + EscapePropertyValue(value));
        }

        private string EscapePropertyValue(string value)
        {
            if (value == null)
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", " ");
        }

        private void WriteLocalVersion(string root, string zipPath)
        {
            string version = GetBuildFromServerPackZip(zipPath);
            if (string.IsNullOrWhiteSpace(version))
                version = Path.GetFileNameWithoutExtension(zipPath);

            File.WriteAllText(Path.Combine(root, VersionMarker), version);
        }

        private string MakeRelativePath(string basePath, string fullPath)
        {
            string baseFull = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string targetFull = Path.GetFullPath(fullPath);
            if (targetFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                return targetFull.Substring(baseFull.Length);
            return targetFull;
        }
    }
}
