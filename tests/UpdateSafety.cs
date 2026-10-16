using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.Plugins;

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
                var before = Snapshot(root);
                string localConfig = Path.Combine(root, "config/Advancedperipherals/settings.toml");
                File.SetAttributes(localConfig, FileAttributes.ReadOnly);
                File.SetAttributes(Path.GetDirectoryName(localConfig), FileAttributes.Directory | FileAttributes.ReadOnly);
                object result = Prepare(source, root, stage =>
                {
                    Unchanged(root, before);
                    FileIs(Path.Combine(stage, "config/Advancedperipherals/settings.toml"), "local=true");
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
                string memory = File.ReadAllText(Path.Combine(root, "user_jvm_args.txt"));
                Check(memory.Contains("-Xms8G") && memory.Contains("-Xmx12G") && !memory.Contains("-Xmx4G"), "Memory reset");
                FileIs(Path.Combine(root, Version), "new");
                FileIs(Path.Combine(root, Defaults, "config/Advancedperipherals/settings.toml"), "pack=true");
                Check(Field(result, "Conflicts") == "2", "Missing config conflict notices");
                Check(File.ReadAllText(Path.Combine(backup, "config-conflicts.txt")).Contains("Advancedperipherals"), "Missing report");
                FileIs(Path.Combine(backup, "incoming-config/config/Advancedperipherals/settings.toml"), "pack=true");
                Check(!File.Exists((string)Call("RecoveryMarker", root)), "Successful update left recovery marker");
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
                Directory.CreateSymbolicLink(Path.Combine(root, "linked-world"), Child("external"));
                try { Prepare(source, root, stage => Task.FromResult(true)); throw new Exception("Followed linked world"); }
                catch (IOException) { FileIs(Child("external/data.txt"), "external data"); }
                Directory.Delete(Path.Combine(root, "linked-world"));
            });
            Console.WriteLine(passed + " update safety checks passed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
