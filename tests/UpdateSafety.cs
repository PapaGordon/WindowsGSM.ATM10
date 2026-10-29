using System;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using ATM10 = WindowsGSM.Plugins.ATM10;

class UpdateSafety
{
    const string Defaults = ".wgsm-atm10-config-defaults";
    const string Version = ".wgsm-atm10-version.txt";
    static int passed;
    static ATM10 plugin = new ATM10(new ServerConfig());
    static string fixture;

    static object Call(string name, params object[] args)
    {
        try { return typeof(ATM10).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plugin, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    static void Write(string path, string contents)
    { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, contents); }
    static void FileIs(string path, string expected)
    { Check(File.Exists(path) && File.ReadAllText(path) == expected, "Wrong contents: " + path); }
    static string Child(string path) { return Path.Combine(fixture, path.Replace('/', Path.DirectorySeparatorChar)); }
    static string MaterialConfig(bool old)
    {
        string originals = Environment.GetEnvironmentVariable("ATM10_MATERIALS_FIXTURES");
        if (!string.IsNullOrEmpty(originals)) return File.ReadAllText(Path.Combine(originals, old ? "8.1.json" : "8.3.json"));
        string[] names = old ? new[] { "iron", "bauxite", "iesnium", "titanium", "tungsten", "xychorium", "yellorite" } : new[] { "iron" };
        return "[" + string.Join(",", names.Select(name => "{\"name\":\"" + name + "\",\"stages\":[\"fragment\",\"gravel\",\"dust\"]}")) + "]";
    }
    static Dictionary<string, string> Snapshot(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            p => p.Substring(root.Length), p => Convert.ToBase64String(File.ReadAllBytes(p)));
    }
    static void Unchanged(string root, Dictionary<string, string> before)
    {
        var after = Snapshot(root);
        Check(before.Count == after.Count && before.All(p => after.ContainsKey(p.Key) && after[p.Key] == p.Value),
            "Existing server was changed by a failed preparation");
    }
    static object Prepare(string source, string root, Func<string, Task<bool>> installer)
    {
        var task = (Task)Call("PrepareAndCommitUpdate", source, root, "new", installer);
        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result").GetValue(task);
    }
    static string Field(object result, string name)
    { return Convert.ToString(result.GetType().GetField(name).GetValue(result)); }
    static string ExistingServer()
    {
        string root = Child("server/serverfiles");
        Write(Path.Combine(root, "config/Advancedperipherals/settings.toml"), "local=true");
        Write(Path.Combine(root, "defaultconfigs/server.toml"), "local-default=true");
        Write(Path.Combine(root, "world/region/r.0.0.mca"), "WORLD");
        Write(Path.Combine(root, "world/serverconfig/server.toml"), "WORLD-CONFIG");
        Write(Path.Combine(root, "server.properties"), "motd=My server\nlevel-name=world\n");
        Write(Path.Combine(root, "user_jvm_args.txt"), "-Xms8G\n-Xmx12G\n");
        Write(Path.Combine(root, "ops.json"), "OPERATOR");
        Write(Path.Combine(root, "mods/old.jar"), "old mod");
        Write(Path.Combine(root, Version), "old");
        using (var hash = SHA256.Create())
        using (var stream = File.OpenRead(Path.Combine(root, "mods/old.jar")))
            Write(Path.Combine(root, Defaults, "pack-mods.sha256"), "version\told\n" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() + "\told.jar\n");
        ServerPath.Root = root;
        return root;
    }
    static string NewPack()
    {
        string source = Child("pack");
        Write(Path.Combine(source, "config/Advancedperipherals/settings.toml"), "pack=true");
        Write(Path.Combine(source, "config/new.toml"), "new-setting=true");
        Write(Path.Combine(source, "defaultconfigs/server.toml"), "pack-default=true");
        Write(Path.Combine(source, "user_jvm_args.txt"), "-Xms2G\n-Xmx4G\n");
        Write(Path.Combine(source, "server.properties"), "motd=pack\n");
        Write(Path.Combine(source, "ops.json"), "[]");
        Write(Path.Combine(source, "world/region/r.0.0.mca"), "DO NOT REPLACE");
        Write(Path.Combine(source, "mods/new.jar"), "new mod");
        Write(Path.Combine(source, "neoforge-new-installer.jar"), "installer");
        Write(Path.Combine(source, "startserver.bat"), "new launcher");
        return source;
    }
    static void Test(string name, Action action)
    {
        fixture = Path.Combine(Path.GetTempPath(), "atm10-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        finally
        {
            foreach (string file in Directory.GetFiles(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            foreach (string dir in Directory.GetDirectories(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(dir, FileAttributes.Directory);
            Directory.Delete(fixture, true);
        }
    }
    static void MergeCase(string name, string old, string local, string next, bool known,
        string expected, int conflictCount)
    {
        Test(name, () =>
        {
            string incoming = Child("new"), current = Child("current"), baseline = Child("old");
            Directory.CreateDirectory(current);
            if (old != null) Write(Path.Combine(baseline, "settings.toml"), old);
            if (local != null) Write(Path.Combine(current, "settings.toml"), local);
            if (next != null) Write(Path.Combine(incoming, "settings.toml"), next);
            var conflicts = new List<string>();
            Call("MergeConfigDirectory", incoming, current, baseline, known, "config", conflicts);
            if (expected == null) Check(!File.Exists(Path.Combine(current, "settings.toml")), "Local deletion lost");
            else FileIs(Path.Combine(current, "settings.toml"), expected);
            Check(conflicts.Count == conflictCount, "Wrong number of conflicts");
        });
    }
    static int Main()
    {
        try
        {
            MergeCase("first upgrade keeps custom configuration", null, "local", "new", false, "local", 1);
            MergeCase("first upgrade adds missing configuration", null, null, "new", false, "new", 0);
            MergeCase("identical first-upgrade configuration has no conflict", null, "new", "new", false, "new", 0);
            MergeCase("unchanged defaults receive pack changes", "old", "old", "new", true, "new", 0);
            MergeCase("custom edits survive changed defaults", "old", "local", "new", true, "local", 1);
            MergeCase("custom edits survive unchanged defaults without conflict", "old", "local", "old", true, "local", 0);
            MergeCase("matching local and incoming edits have no conflict", "old", "new", "new", true, "new", 0);
            MergeCase("local deletion survives changed defaults", "old", null, "new", true, null, 1);
            MergeCase("local deletion survives unchanged defaults", "old", null, "old", true, null, 0);
            MergeCase("removed unmodified default is removed", "old", "old", null, true, null, 0);
            MergeCase("removed customized default is kept", "old", "local", null, true, "local", 1);
            MergeCase("new default colliding with local-only file is kept", null, "local", "new", true, "local", 1);
            Test("file/directory conflicts keep local data", () =>
            {
                Write(Child("current/nested"), "local file");
                Write(Child("new/nested/settings.toml"), "new default");
                Directory.CreateDirectory(Child("current/was-a-file.toml"));
                Write(Child("new/was-a-file.toml"), "new file");
                var conflicts = new List<string>();
                Call("MergeConfigDirectory", Child("new"), Child("current"), Child("old"), false, "config", conflicts);
                FileIs(Child("current/nested"), "local file");
                Check(Directory.Exists(Child("current/was-a-file.toml")) && conflicts.Count == 2, "Type conflict not preserved");
            });
            Test("successful update retains full backup, configs, worlds, operators and memory", () =>
            {
                string root = ExistingServer(), source = NewPack();
                string jvmArgs = "# server JVM args\n-Xms8G\n-Xmx12G\n-XX:+UseG1GC\n-Dmy.setting=keep-this\n";
                Write(Path.Combine(root, "user_jvm_args.txt"), jvmArgs);
                var before = Snapshot(root);
                string localConfig = Path.Combine(root, "config/Advancedperipherals/settings.toml");
                File.SetAttributes(localConfig, FileAttributes.ReadOnly);
                File.SetAttributes(Path.GetDirectoryName(localConfig), FileAttributes.Directory | FileAttributes.ReadOnly);
                object result = Prepare(source, root, stage =>
                {
                    Unchanged(root, before);
                    FileIs(Path.Combine(stage, "config/Advancedperipherals/settings.toml"), "local=true");
                    FileIs(Path.Combine(stage, "user_jvm_args.txt"), jvmArgs);
                    // Check if the updater can recover from an installer rewriting this file.
                    Write(Path.Combine(stage, "user_jvm_args.txt"), "-Xmx4G\n");
                    Write(Path.Combine(stage, "libraries/runtime.txt"), "runtime");
                    return Task.FromResult(true);
                });
                string backup = Field(result, "BackupPath");
                Unchanged(Path.Combine(backup, "serverfiles"), before);
                FileIs(Path.Combine(root, "config/Advancedperipherals/settings.toml"), "local=true");
                FileIs(Path.Combine(root, "defaultconfigs/server.toml"), "local-default=true");
                FileIs(Path.Combine(root, "config/new.toml"), "new-setting=true");
                FileIs(Path.Combine(root, "world/region/r.0.0.mca"), "WORLD");
                FileIs(Path.Combine(root, "world/serverconfig/server.toml"), "WORLD-CONFIG");
                FileIs(Path.Combine(root, "ops.json"), "OPERATOR");
                FileIs(Path.Combine(root, "mods/new.jar"), "new mod");
                Check(!File.Exists(Path.Combine(root, "mods/old.jar")), "Stale mod survived");
                FileIs(Path.Combine(root, "user_jvm_args.txt"), jvmArgs);
                FileIs(Path.Combine(root, Version), "new");
                FileIs(Path.Combine(root, Defaults, "config/Advancedperipherals/settings.toml"), "pack=true");
                Check(Field(result, "Conflicts") == "2", "Missing config conflict notices");
                Check(File.ReadAllText(Path.Combine(backup, "config-conflicts.txt")).Contains("Advancedperipherals"), "Missing report");
                FileIs(Path.Combine(backup, "incoming-config/config/Advancedperipherals/settings.toml"), "pack=true");
                Check(!File.Exists((string)Call("RecoveryMarker", root)), "Successful update left recovery marker");
            });
            Test("update adds the pack JVM args file when none exists", () =>
            {
                string root = ExistingServer(), source = NewPack();
                File.Delete(Path.Combine(root, "user_jvm_args.txt"));
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "user_jvm_args.txt"), "-Xms2G\n-Xmx4G\n");
            });
            Test("reinstall keeps the entire JVM args file", () =>
            {
                string root = ExistingServer(), source = NewPack();
                string jvmArgs = "# my JVM options\n-Xms8G\n-Xmx12G\n-XX:+UseG1GC\n";
                Write(Path.Combine(root, "user_jvm_args.txt"), jvmArgs);
                string packZip = Child("ServerFiles-new.zip");
                ZipFile.CreateFromDirectory(source, packZip);
                ((Task)Call("ExtractServerPack", packZip, root)).GetAwaiter().GetResult();
                FileIs(Path.Combine(root, "user_jvm_args.txt"), jvmArgs);
            });
            Test("fresh install takes the pack JVM args file", () =>
            {
                string root = Child("new-server/serverfiles"), source = NewPack();
                string packZip = Child("ServerFiles-new.zip");
                ZipFile.CreateFromDirectory(source, packZip);
                Directory.CreateDirectory(root);
                ((Task)Call("ExtractServerPack", packZip, root)).GetAwaiter().GetResult();
                FileIs(Path.Combine(root, "user_jvm_args.txt"), "-Xms2G\n-Xmx4G\n");
            });
            Test("installer failure leaves every original byte untouched", () =>
            {
                string root = ExistingServer(), source = NewPack();
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(false)); throw new Exception("Update accepted failure"); }
                catch (IOException) { Unchanged(root, before); }
                Check(!File.Exists((string)Call("RecoveryMarker", root)), "Preparation failure blocked startup");
            });
            Test("copy failure leaves original server untouched", () =>
            {
                string root = ExistingServer(), source = NewPack();
                var before = Snapshot(root);
                Directory.CreateDirectory(Path.Combine(root, "startserver.bat"));
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Update accepted copy failure"); }
                catch (IOException) { Unchanged(root, before); }
            });
            Test("failed activation restores original directory", () =>
            {
                string root = ExistingServer(), backup = Child("backup");
                Directory.CreateDirectory(backup);
                var before = Snapshot(root);
                try { Call("PublishPreparedUpdate", root, Child("missing-stage"), backup); throw new Exception("Activation accepted missing stage"); }
                catch (IOException) { Unchanged(root, before); }
                Check(!Directory.Exists(Path.Combine(backup, "serverfiles")), "Restored server still in backup");
                Check(!File.Exists((string)Call("RecoveryMarker", root)), "Rollback left recovery marker");
            });
            Test("custom world name is retained even when pack has that directory", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "server.properties"), "level-name=MyWorld\n");
                Write(Path.Combine(root, "MyWorld/data.txt"), "custom world");
                Write(Path.Combine(source, "MyWorld/data.txt"), "pack content");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "MyWorld/data.txt"), "custom world");
            });
            Test("Neo Vitae material registry follows the new pack and retains the old file in backup", () =>
            {
                string root = ExistingServer(), source = NewPack();
                const string relative = "config/neovitae/materials.json";
                Write(Path.Combine(root, relative), MaterialConfig(true));
                Write(Path.Combine(source, relative), MaterialConfig(false));
                object result = Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, relative), MaterialConfig(false));
                FileIs(Path.Combine(Field(result, "BackupPath"), "serverfiles", relative), MaterialConfig(true));
                FileIs(Path.Combine(root, "config/Advancedperipherals/settings.toml"), "local=true");
                Check(!File.ReadAllText(Path.Combine(Field(result, "BackupPath"), "config-conflicts.txt")).Contains("neovitae/materials.json"), "The report incorrectly says the old registry definition was kept");
            });
            Test("same pack version still needs repair when the material definition differs", () =>
            {
                string root = ExistingServer(), source = NewPack();
                const string relative = "config/neovitae/materials.json";
                Write(Path.Combine(root, relative), MaterialConfig(true));
                Write(Path.Combine(source, relative), MaterialConfig(false));
                string zip = Child("ServerFiles-8.3.zip"); ZipFile.CreateFromDirectory(source, zip);
                Check(!(bool)Call("IsPackCurrent", root, zip, "8.3", "8.3"), "Same-version gate hides the stale material registry");
                Write(Path.Combine(root, relative), MaterialConfig(false));
                var before = Snapshot(root);
                Check((bool)Call("IsPackCurrent", root, zip, "8.3", "8.3"), "Matching registry data forces needless repeated reinstalls");
                Unchanged(root, before);
            });
            Test("wrapped server packs check their own material file during same-version repair", () =>
            {
                string root = ExistingServer(), source = Path.Combine(Child("wrapped"), "ServerFiles");
                Write(Path.Combine(source, "startserver.bat"), "launcher");
                Write(Path.Combine(source, "config/neovitae/materials.json"), MaterialConfig(false));
                string zip = Child("ServerFiles-8.3.zip"); ZipFile.CreateFromDirectory(Child("wrapped"), zip);
                Check(!(bool)Call("IsPackCurrent", root, zip, "8.3", "8.3"), "Missing registry config was treated as current");
                Write(Path.Combine(root, "config/neovitae/materials.json"), MaterialConfig(false));
                Check((bool)Call("IsPackCurrent", root, zip, "8.3", "8.3"), "Wrapper path prevents registry comparison");
            });
            Test("pack-owned registry definitions replace local edits with the old file backed up", () =>
            {
                string root = ExistingServer(), source = NewPack();
                const string relative = "config/neovitae/materials.json";
                Write(Path.Combine(source, relative), MaterialConfig(true));
                Call("SaveConfigDefaults", source, root, "old");
                Write(Path.Combine(root, relative), "[{\"name\":\"local_material\"}]");
                Write(Path.Combine(source, relative), MaterialConfig(false));
                object result = Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, relative), MaterialConfig(false));
                FileIs(Path.Combine(Field(result, "BackupPath"), "serverfiles", relative), "[{\"name\":\"local_material\"}]");
                Check(File.ReadAllText(Path.Combine(Field(result, "BackupPath"), "pack-managed-configs.txt")).Contains(relative), "Missing pack-managed configuration report");
            });
            Test("failed runtime preparation leaves the original material registry and world unchanged", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "config/neovitae/materials.json"), MaterialConfig(true));
                Write(Path.Combine(source, "config/neovitae/materials.json"), MaterialConfig(false));
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(false)); throw new Exception("Failed runtime activated"); }
                catch (IOException) { Unchanged(root, before); }
            });
            Test("a directory blocking the material file is refused before activation", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Directory.CreateDirectory(Path.Combine(root, "config/neovitae/materials.json"));
                Write(Path.Combine(source, "config/neovitae/materials.json"), MaterialConfig(false));
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Removed a blocking directory"); }
                catch (IOException e) { Check(e.Message.Contains("registry definition"), "Missing registry conflict explanation"); Unchanged(root, before); }
            });
            Test("a pack without a material definition keeps the existing file", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "config/neovitae/materials.json"), MaterialConfig(true));
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "config/neovitae/materials.json"), MaterialConfig(true));
            });
            Test("baseline persists across updates and detects later user edits", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Call("SaveConfigDefaults", source, root, "old");
                Write(Path.Combine(root, "config/new.toml"), "new-setting=true");
                Write(Path.Combine(source, "config/new.toml"), "changed pack default");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "config/new.toml"), "changed pack default");
                Write(Path.Combine(root, "config/new.toml"), "my new edit");
                Write(Path.Combine(source, "config/new.toml"), "another pack change");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "config/new.toml"), "my new edit");
            });
            Test("mismatched baseline falls back to preservation", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Call("SaveConfigDefaults", source, root, "different-version");
                Check(!(bool)Call("HasConfigDefaults", root), "Stale defaults accepted");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "config/Advancedperipherals/settings.toml"), "local=true");
            });
            Test("recovery marker prevents start and another update", () =>
            {
                string root = ExistingServer();
                Write((string)Call("RecoveryMarker", root), "recover me");
                try { ((FileStream)Call("AcquireUpdateLock", root)).Dispose(); throw new Exception("Pending recovery ignored"); }
                catch (IOException) { }
                ServerPath.Root = root;
                Check(plugin.Start().GetAwaiter().GetResult() == null && plugin.Error.Contains("recovery"), "Start ignored recovery");
            });
            Test("concurrent update lock is exclusive", () =>
            {
                string root = ExistingServer();
                using ((FileStream)Call("AcquireUpdateLock", root))
                {
                    try { ((FileStream)Call("AcquireUpdateLock", root)).Dispose(); throw new Exception("Concurrent update allowed"); }
                    catch (IOException) { }
                }
            });
            Test("linked directories are refused before modifying originals", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Directory.CreateDirectory(Child("external"));
                Write(Child("external/data.txt"), "external data");
                try { Directory.CreateSymbolicLink(Path.Combine(root, "linked-world"), Child("external")); }
                catch (IOException e) when ((e.HResult & 0xffff) == 1314)
                {
                    // A junction exercises the same reparse-point guard without symlink privilege.
                    using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" +
                        Path.Combine(root, "linked-world") + "\" \"" + Child("external") + "\"")
                        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    {
                        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        if (process.ExitCode != 0) throw new IOException("Could not create a linked-directory fixture: " + output);
                    }
                    Console.WriteLine("INFO Using a junction fixture; symbolic-link privilege is unavailable.");
                }
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Followed linked world"); }
                catch (IOException) { FileIs(Child("external/data.txt"), "external data"); }
                Directory.Delete(Path.Combine(root, "linked-world"));
            });
            Test("BCC identity advances from 8.1 to 8.3 without overwriting custom settings", () =>
            {
                string root = ExistingServer(), source = NewPack();
                ServerPath.Root = root;
                Write(Path.Combine(root, "config/bcc-common.toml"), "[general]\nmodpackName = \"All the Mods 10\"\nmodpackVersion = \"8.1\"\nuseMetadata = false\ncustomSetting = true\n");
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackName = \"All the Mods 10\"\nmodpackVersion = \"8.3\"\nuseMetadata = false\ncustomSetting = false\n");
                Prepare(source, root, stage => Task.FromResult(true));
                string updated = File.ReadAllText(Path.Combine(root, "config/bcc-common.toml"));
                Check(updated.Contains("modpackVersion = \"8.3\""), "Server still advertises 8.1 after the 8.3 update");
                Check(updated.Contains("customSetting = true"), "A local setting was overwritten");
                Check(plugin.GetLocalBuild() == "8.3", "Local build does not match the advertised pack identity");
            });
            Test("advertised compatibility identity takes precedence over an optimistic marker", () =>
            {
                string root = ExistingServer(); ServerPath.Root = root;
                Write(Path.Combine(root, Version), "8.3");
                Write(Path.Combine(root, "config/bcc-common.toml"), "[general]\nmodpackVersion = \"8.1\"\nuseMetadata = false\n");
                Check(plugin.GetLocalBuild() == "8.1", "Marker hides the old advertised pack identity");
            });
            Test("server pack build is read from its identity instead of a renamed ZIP", () =>
            {
                string source = NewPack();
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackVersion = \"8.3\"\nuseMetadata = false\n");
                string zip = Child("ServerFiles-8.1.zip"); ZipFile.CreateFromDirectory(source, zip);
                Check((string)Call("GetBuildFromServerPackZip", zip) == "8.3", "ZIP name overrides the pack's real identity");
            });
            Test("available local update is reported before any web lookup", () =>
            {
                string root = ExistingServer(), source = NewPack(); ServerPath.Root = root;
                Write(Path.Combine(root, Version), "8.1");
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackVersion = \"8.3\"\nuseMetadata = false\n");
                ZipFile.CreateFromDirectory(source, Path.Combine(root, "ServerFiles-8.3.zip"));
                Check(plugin.GetRemoteBuild().GetAwaiter().GetResult() == "8.3", "The available update is incorrectly reported as 8.1");
            });
            Test("additional custom mods survive while obsolete pack mods are removed", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "mods/MyAdditionalMod-1.0.jar"), "own extra mod");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "mods/MyAdditionalMod-1.0.jar"), "own extra mod");
                FileIs(Path.Combine(root, "mods/new.jar"), "new mod");
                Check(!File.Exists(Path.Combine(root, "mods/old.jar")), "Obsolete official pack mod was left behind");
                Check(File.ReadAllText(Path.Combine(root, Defaults, "pack-mods.sha256")).Contains("new.jar"), "New pack ownership inventory was not saved");
                Check(!File.ReadAllText(Path.Combine(root, Defaults, "pack-mods.sha256")).Contains("MyAdditionalMod"), "Additional mod was incorrectly claimed by the pack");
            });
            Test("an additional mod filename collision aborts without changing live files", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "mods/new.jar"), "my own unrelated mod");
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Overwrote additional mod"); }
                catch (IOException e) { Check(e.Message.Contains("conflicts"), "Missing conflict explanation"); Unchanged(root, before); }
            });
            Test("a changed old pack mod is never silently removed", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(root, "mods/old.jar"), "changed after installation");
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Removed changed file"); }
                catch (IOException) { Unchanged(root, before); }
            });
            Test("first migration uses the previous original ZIP to identify pack-owned mods", () =>
            {
                string root = ExistingServer(), source = NewPack();
                File.Delete(Path.Combine(root, Defaults, "pack-mods.sha256"));
                string oldPack = Child("old-original");
                Write(Path.Combine(oldPack, "startserver.bat"), "original launcher");
                Write(Path.Combine(oldPack, "mods/old.jar"), "old mod");
                ZipFile.CreateFromDirectory(oldPack, Path.Combine(root, "ServerFiles-old.zip"));
                Write(Path.Combine(root, "mods/MyAdditionalMod.jar"), "additional");
                Prepare(source, root, stage => Task.FromResult(true));
                FileIs(Path.Combine(root, "mods/MyAdditionalMod.jar"), "additional");
                Check(!File.Exists(Path.Combine(root, "mods/old.jar")), "Old official mod survived migration");
            });
            Test("unknown old mods need provenance before an update can delete anything", () =>
            {
                string root = ExistingServer(), source = NewPack();
                File.Delete(Path.Combine(root, Defaults, "pack-mods.sha256"));
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Guessed which mods were custom"); }
                catch (IOException e) { Check(e.Message.Contains("previous original"), "No migration instructions"); Unchanged(root, before); }
            });
            Test("an older JAR with the same pack mod ID is replaced even outside the inventory", () =>
            {
                string root = ExistingServer(), source = NewPack();
                foreach (string jar in new[] { Path.Combine(source, "mods/Official-2.jar"), Path.Combine(root, "mods/Additional-1.jar") })
                using (var archive = ZipFile.Open(jar, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"same_mod\"\nversion=\"1\"\n");
                Prepare(source, root, stage => Task.FromResult(true));
                Check(!File.Exists(Path.Combine(root, "mods/Additional-1.jar")), "Older version of a pack mod survived");
                Check(File.Exists(Path.Combine(root, "mods/Official-2.jar")), "Incoming pack mod is missing");
            });
            Test("an overlapping bundle with additional mod IDs is never silently removed", () =>
            {
                string root = ExistingServer(), source = NewPack();
                using (var archive = ZipFile.Open(Path.Combine(source, "mods/Official-2.jar"), ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"same_mod\"\n");
                using (var archive = ZipFile.Open(Path.Combine(root, "mods/Additional-1.jar"), ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"same_mod\"\n[[mods]]\nmodId=\"extra_mod\"\n");
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Removed an additional bundled mod ID"); }
                catch (IOException e) { Check(e.Message.Contains("additional mod IDs"), "Conflict did not explain the bundle"); Unchanged(root, before); }
            });
            Test("legacy computercraft 1.113.1 is replaced by pack 1.120.2 while an extra mod survives", () =>
            {
                string root = ExistingServer(), source = NewPack();
                string older = "cc-tweaked-1.21.1-forge-1.113.1.jar", newer = "cc-tweaked-1.21.1-forge-1.120.2.jar";
                foreach (string jar in new[] { Path.Combine(root, "mods", older), Path.Combine(source, "mods", newer) })
                using (var archive = ZipFile.Open(jar, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"computercraft\"\nversion=\"fixture\"\n");
                Write(Path.Combine(root, "mods/MyExtra.jar"), "keep me");
                Prepare(source, root, stage => Task.FromResult(true));
                Check(!File.Exists(Path.Combine(root, "mods", older)), "Legacy CC:Tweaked version survived");
                Check(File.Exists(Path.Combine(root, "mods", newer)), "Current CC:Tweaked version is missing");
                FileIs(Path.Combine(root, "mods/MyExtra.jar"), "keep me");
            });
            Test("official pack versions of the same mod are both kept for NeoForge discovery", () =>
            {
                string root = ExistingServer(), source = NewPack();
                string older = "cc-tweaked-1.21.1-forge-1.113.1.jar", newer = "cc-tweaked-1.21.1-forge-1.120.2.jar";
                foreach (string name in new[] { older, newer })
                using (var archive = ZipFile.Open(Path.Combine(source, "mods", name), ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"computercraft\"\nversion=\"" + (name == older ? "1.113.1" : "1.120.2") + "\"\n");
                Write(Path.Combine(root, "mods/MyExtra.jar"), "additional");
                var originalPack = Snapshot(source);
                Prepare(source, root, stage => Task.FromResult(true));
                foreach (string name in new[] { older, newer })
                    Check(File.ReadAllBytes(Path.Combine(root, "mods", name)).SequenceEqual(File.ReadAllBytes(Path.Combine(source, "mods", name))), "A pack-supplied candidate was removed or changed: " + name);
                FileIs(Path.Combine(root, "mods/MyExtra.jar"), "additional");
                string inventory = File.ReadAllText(Path.Combine(root, Defaults, "pack-mods.sha256"));
                Check(inventory.Contains(older) && inventory.Contains(newer), "Pack candidates were not both recorded as owned");
                Unchanged(source, originalPack);
            });
            Test("pack candidate support does not hide duplicate additional mods", () =>
            {
                string root = ExistingServer(), source = NewPack();
                foreach (string name in new[] { "Extra-1.jar", "Extra-2.jar" })
                using (var archive = ZipFile.Open(Path.Combine(root, "mods", name), ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"additional_mod\"\n");
                var before = Snapshot(root);
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Unexpected additional duplicate accepted"); }
                catch (IOException e) { Check(e.Message.Contains("Duplicate mod ID additional_mod"), "Missing additional mod conflict"); Unchanged(root, before); }
            });
            Test("shared dependency IDs do not count as duplicate installed mods", () =>
            {
                string root = ExistingServer(), source = NewPack();
                foreach (string jar in new[] { Path.Combine(source, "mods/Official-2.jar"), Path.Combine(root, "mods/Additional-1.jar") })
                using (var archive = ZipFile.Open(jar, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("META-INF/neoforge.mods.toml").Open()))
                    writer.Write("[[mods]]\nmodId=\"" + (jar.Contains("Official") ? "official_mod" : "additional_mod") + "\"\n[[dependencies.example]]\nmodId=\"minecraft\"\n[[dependencies.example]]\nmodId=\"neoforge\"\n");
                Prepare(source, root, stage => Task.FromResult(true));
                Check(File.Exists(Path.Combine(root, "mods/Additional-1.jar")), "Additional mod was lost");
            });
            Test("an already-updated 8.3 marker repairs stale BCC using the original 8.3 ZIP", () =>
            {
                string root = ExistingServer(), source = NewPack();
                File.Delete(Path.Combine(root, Defaults, "pack-mods.sha256"));
                File.Delete(Path.Combine(root, "mods/old.jar"));
                Write(Path.Combine(root, Version), "8.3");
                Write(Path.Combine(root, "mods/new.jar"), "new mod");
                Write(Path.Combine(root, "mods/Additional.jar"), "additional");
                Write(Path.Combine(root, "config/bcc-common.toml"), "[general]\nmodpackVersion=\"8.1\"\nuseMetadata=false\n");
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackVersion=\"8.3\"\nuseMetadata=false\n");
                ZipFile.CreateFromDirectory(source, Path.Combine(root, "ServerFiles-8.3.zip"));
                Prepare(source, root, stage => Task.FromResult(true));
                Check(plugin.GetLocalBuild() == "8.3", "The stale advertised version was not repaired");
                FileIs(Path.Combine(root, "mods/Additional.jar"), "additional");
                FileIs(Path.Combine(root, "mods/new.jar"), "new mod");
            });
            Test("the old migration ZIP cannot select a downgrade just because it was copied later", () =>
            {
                string root = ExistingServer(), source = NewPack();
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackVersion=\"8.3\"\nuseMetadata=false\n");
                ZipFile.CreateFromDirectory(source, Path.Combine(root, "ServerFiles-8.3.zip"));
                Write(Path.Combine(source, "config/bcc-common.toml"), "[general]\nmodpackVersion=\"8.1\"\nuseMetadata=false\n");
                string oldZip = Path.Combine(root, "ServerFiles-8.1.zip");
                ZipFile.CreateFromDirectory(source, oldZip);
                File.SetLastWriteTimeUtc(oldZip, DateTime.UtcNow.AddHours(1));
                Check(plugin.GetRemoteBuild().GetAwaiter().GetResult() == "8.3", "The old ownership archive selected a downgrade");
            });
            Console.WriteLine(passed + " update safety checks passed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
