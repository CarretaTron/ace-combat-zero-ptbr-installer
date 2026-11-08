using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;
using Ac0PtBrInstaller;

internal static class IntegrationTests
{
    private const string PendingName = ".ac0_ptbr_instalacao_pendente.json";
    private static string root, fixture, variant;
    private static int sequence;
    private static readonly List<object> results = new List<object>();
    private static byte[] Bytes(string key) { return File.ReadAllBytes(Path.Combine(fixture, key + ".bin")); }
    private static string Hash(string path) { return InstallerEngine.HashFile(path); }
    private static void Check(bool condition, string detail) { if (!condition) throw new Exception(detail); }
    private static void ResetHooks()
    {
        InstallerEngine.BeforeReplaceForTests = null;
        InstallerEngine.GameRunningForTests = delegate { return false; };
        InstallerEngine.FreeSpaceForTests = delegate { return Int64.MaxValue; };
    }
    private static string Game(string label, string state)
    {
        string directory = Path.Combine(root, (++sequence).ToString("000") + "_" + label);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "acz.exe"), Bytes("exe"));
        File.WriteAllBytes(Path.Combine(directory, "a.arc"), Bytes(state + "_a"));
        File.WriteAllBytes(Path.Combine(directory, "d.arc"), Bytes(state + "_d"));
        return directory;
    }
    private static string HashFixture(string key) { return Hash(Path.Combine(fixture, key + ".bin")); }
    private static void StateIs(string game, string state)
    {
        Check(Hash(Path.Combine(game, "a.arc")) == HashFixture(state + "_a"), "a.arc does not match " + state);
        Check(Hash(Path.Combine(game, "d.arc")) == HashFixture(state + "_d"), "d.arc does not match " + state);
        Check(Hash(Path.Combine(game, "acz.exe")) == HashFixture("exe"), "The executable changed");
    }
    private static int BackupCount(string game)
    {
        string backup = Path.Combine(game, "Backup_PTBR");
        return Directory.Exists(backup) ? Directory.GetDirectories(backup).Length : 0;
    }
    private static void NoStage(string game)
    {
        Check(Directory.GetDirectories(game, ".ac0-ptbr-temporario-*").Length == 0, "Staging directory remains");
        Check(Directory.GetFiles(game, ".ac0-ptbr-restaurar-*.tmp").Length == 0, "Restore temporary file remains");
    }
    private static Exception Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error)
        {
            Check(error.Message.IndexOf(message, StringComparison.OrdinalIgnoreCase) >= 0,
                "Expected message '" + message + "', found '" + error.Message + "'");
            return error;
        }
        throw new Exception("The operation should have failed: " + message);
    }
    private static void Junction(string link, string target)
    {
        ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"");
        info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using (Process process = Process.Start(info))
        {
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit();
            Check(process.ExitCode == 0, "Could not create fixture junction: " + output);
        }
        Check((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, "Fixture junction is not reparse point");
    }
    private static void Test(string name, Action action)
    {
        ResetHooks();
        try { action(); results.Add(new { name = name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception error) { results.Add(new { name = name, passed = false, error = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error.Message); }
        finally { ResetHooks(); }
    }
    private static InstallTransaction Pending(string game, string oldState)
    {
        string backup = Path.Combine(game, "Backup_PTBR", "interrompida");
        Directory.CreateDirectory(backup);
        File.WriteAllBytes(Path.Combine(backup, "a.arc"), Bytes(oldState + "_a"));
        File.WriteAllBytes(Path.Combine(backup, "d.arc"), Bytes(oldState + "_d"));
        InstallTransaction transaction = new InstallTransaction {
            FormatVersion = 1, PackageVersion = "fixture_008", GameDirectory = game,
            BackupDirectory = backup, CreatedAt = DateTime.Now.ToString("o"), Status = "instalando",
            OriginalASha256 = HashFixture(oldState + "_a"), OriginalDSha256 = HashFixture(oldState + "_d"),
            InstalledASha256 = HashFixture("target_a"), InstalledDSha256 = HashFixture("target_d")
        };
        InstallerEngine.WriteJson(Path.Combine(game, PendingName), transaction);
        return transaction;
    }
    private static void NormalTests()
    {
        Test("embedded_package_sha", delegate {
            Dictionary<string, object> result = InstallerEngine.VerifyPackage(null);
            Check((bool)result["ok"], "Package verification failed");
        });
        Test("successful_install_exact_backup", delegate {
            string game = Game("successful", "original");
            List<int> progress = new List<int>();
            InstallResult result = InstallerEngine.Install(game, delegate(InstallProgress p) { progress.Add(p.Percent); });
            Check(!result.AlreadyInstalled, "Fresh install claimed no-op"); StateIs(game, "target");
            Check(BackupCount(game) == 1 && Directory.Exists(result.BackupDirectory), "Verified backup missing");
            Check(Hash(Path.Combine(result.BackupDirectory, "a.arc")) == HashFixture("original_a"), "A backup differs");
            Check(Hash(Path.Combine(result.BackupDirectory, "d.arc")) == HashFixture("original_d"), "D backup differs");
            Check(File.ReadAllText(Path.Combine(result.BackupDirectory, "backup.json")).Contains("instalado"), "Backup journal was not completed");
            Check(File.Exists(Path.Combine(result.BackupDirectory, "LEIA-ME.txt")), "Recovery instructions missing");
            Check(progress[progress.Count - 1] == 100, "Progress did not complete");
            Check(!File.Exists(Path.Combine(game, PendingName)), "Completed transaction remains pending"); NoStage(game);
        });
        Test("repeated_install_no_backup_or_mutation", delegate {
            string game = Game("repeat", "original"); InstallerEngine.Install(game, null);
            string[] backup = Directory.GetDirectories(Path.Combine(game, "Backup_PTBR"));
            InstallResult result = InstallerEngine.Install(game, null);
            Check(result.AlreadyInstalled && result.BackupDirectory == null, "Repeated install was not no-op");
            Check(BackupCount(game) == backup.Length, "Repeat created another backup"); StateIs(game, "target"); NoStage(game);
        });
        Test("existing_translation_upgrade", delegate {
            string game = Game("upgrade", "previous"); InstallResult result = InstallerEngine.Install(game, null);
            StateIs(game, "target");
            Check(Hash(Path.Combine(result.BackupDirectory, "a.arc")) == HashFixture("previous_a"), "Previous A backup differs");
            Check(Hash(Path.Combine(result.BackupDirectory, "d.arc")) == HashFixture("previous_d"), "Previous D backup differs");
        });
        Test("parent_ACZero_and_accented_path", delegate {
            string parent = Path.Combine(root, "Espaços e tradução çã"); string game = Path.Combine(parent, "ACZero");
            Directory.CreateDirectory(game);
            File.WriteAllBytes(Path.Combine(game, "acz.exe"), Bytes("exe"));
            File.WriteAllBytes(Path.Combine(game, "a.arc"), Bytes("original_a"));
            File.WriteAllBytes(Path.Combine(game, "d.arc"), Bytes("original_d"));
            Check(InstallerEngine.ResolveGameDirectory("  \"" + parent + "\"  ") == game, "Parent was not resolved");
            Check(InstallerEngine.Install(parent, null).GameDirectory == game, "Install used wrong child"); StateIs(game, "target");
        });
        Test("empty_missing_and_wrong_folder", delegate {
            Reject(delegate { InstallerEngine.Install("", null); }, "Escolha");
            Reject(delegate { InstallerEngine.Install(Path.Combine(root, "does_not_exist"), null); }, "não existe");
            string game = Game("missing", "original"); File.Delete(Path.Combine(game, "d.arc"));
            Reject(delegate { InstallerEngine.Install(game, null); }, "Selecione"); Check(BackupCount(game) == 0, "Invalid folder created backup");
        });
        Test("updated_executable_hash_same_size_accepted_and_preserved", delegate {
            string game = Game("wrong_exe", "original"); byte[] exe = Bytes("exe"); exe[0] ^= 1; File.WriteAllBytes(Path.Combine(game, "acz.exe"), exe);
            string executableHash = Hash(Path.Combine(game, "acz.exe"));
            InstallResult result = InstallerEngine.Install(game, null);
            Check(!result.AlreadyInstalled && BackupCount(game) == 1, "Updated executable blocked installation");
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("target_a"), "Updated executable blocked A");
            Check(Hash(Path.Combine(game, "d.arc")) == HashFixture("target_d"), "Updated executable blocked D");
            Check(Hash(Path.Combine(game, "acz.exe")) == executableHash, "Updated executable was modified"); NoStage(game);
        });
        Test("updated_executable_length_accepted_and_repeat_no_op", delegate {
            string game = Game("wrong_length", "original"); File.AppendAllText(Path.Combine(game, "acz.exe"), "extra");
            string executableHash = Hash(Path.Combine(game, "acz.exe"));
            InstallResult result = InstallerEngine.Install(game, null);
            Check(!result.AlreadyInstalled && BackupCount(game) == 1, "New executable size blocked installation");
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("target_a"), "New executable size blocked A");
            Check(Hash(Path.Combine(game, "d.arc")) == HashFixture("target_d"), "New executable size blocked D");
            Check(Hash(Path.Combine(game, "acz.exe")) == executableHash, "Updated executable was modified during install");
            File.AppendAllText(Path.Combine(game, "acz.exe"), "another update");
            executableHash = Hash(Path.Combine(game, "acz.exe"));
            Check(InstallerEngine.Install(game, null).AlreadyInstalled, "Executable update blocked repeat no-op");
            Check(BackupCount(game) == 1, "Repeat with updated executable created backup");
            Check(Hash(Path.Combine(game, "acz.exe")) == executableHash, "Updated executable was modified"); NoStage(game);
        });
        Test("unknown_archive_hash", delegate {
            string game = Game("unknown", "original"); File.AppendAllText(Path.Combine(game, "a.arc"), "unknown"); string hash = Hash(Path.Combine(game, "a.arc"));
            Reject(delegate { InstallerEngine.Install(game, null); }, "não correspondem");
            Check(Hash(Path.Combine(game, "a.arc")) == hash, "Unknown A changed"); Check(BackupCount(game) == 0, "Unknown A created backup");
        });
        Test("mixed_known_versions_rejected", delegate {
            string game = Game("mixed", "original"); File.WriteAllBytes(Path.Combine(game, "d.arc"), Bytes("previous_d"));
            Reject(delegate { InstallerEngine.Install(game, null); }, "não correspondem");
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("original_a"), "Mixed pair changed A");
            Check(Hash(Path.Combine(game, "d.arc")) == HashFixture("previous_d"), "Mixed pair changed D"); Check(BackupCount(game) == 0, "Mixed pair created backup");
        });
        Test("game_open_before_install", delegate {
            string game = Game("running", "original"); InstallerEngine.GameRunningForTests = delegate { return true; };
            Reject(delegate { InstallerEngine.Install(game, null); }, "Feche o jogo"); StateIs(game, "original"); Check(BackupCount(game) == 0, "Running game created backup");
        });
        Test("game_opens_during_preparation", delegate {
            string game = Game("opens", "original"); int calls = 0;
            InstallerEngine.GameRunningForTests = delegate { return ++calls >= 2; };
            Reject(delegate { InstallerEngine.Install(game, null); }, "Feche o jogo"); StateIs(game, "original"); NoStage(game);
            Check(!File.Exists(Path.Combine(game, PendingName)), "Preparation left journal");
        });
        Test("game_opens_before_commit", delegate {
            string game = Game("opens_commit", "original"); int calls = 0;
            InstallerEngine.GameRunningForTests = delegate { return ++calls >= 3; };
            Reject(delegate { InstallerEngine.Install(game, null); }, "Feche o jogo"); StateIs(game, "original"); NoStage(game);
            Check(!File.Exists(Path.Combine(game, PendingName)), "Precommit left journal");
        });
        Test("read_only_archive", delegate {
            string game = Game("read_only", "original"); string path = Path.Combine(game, "d.arc");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try { Reject(delegate { InstallerEngine.Install(game, null); }, "somente leitura"); StateIs(game, "original"); Check(BackupCount(game) == 0, "Readonly created backup"); }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        });
        Test("insufficient_disk_space", delegate {
            string game = Game("space", "original"); InstallerEngine.FreeSpaceForTests = delegate { return 0; };
            Reject(delegate { InstallerEngine.Install(game, null); }, "espaço livre suficiente"); StateIs(game, "original"); NoStage(game); Check(BackupCount(game) == 0, "No space created backup");
        });
        Test("actual_write_permission_denied", delegate {
            string game = Game("permission_denied", "original"); DirectorySecurity original = Directory.GetAccessControl(game); DirectorySecurity denied = Directory.GetAccessControl(game);
            SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
            denied.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.CreateDirectories, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
            Directory.SetAccessControl(game, denied);
            try { Reject(delegate { InstallerEngine.Install(game, null); }, "Sem permissão"); StateIs(game, "original"); NoStage(game); Check(BackupCount(game) == 0, "Denied folder created backup"); }
            finally { Directory.SetAccessControl(game, original); }
        });
        Test("backup_root_junction_rejected_before_install", delegate {
            string game = Game("root_junction", "original"); string outside = Path.Combine(root, "root_junction_outside"); Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "marker.txt"), "untouched"); Junction(Path.Combine(game, "Backup_PTBR"), outside);
            Reject(delegate { InstallerEngine.Install(game, null); }, "não pode ser um link"); StateIs(game, "original"); NoStage(game);
            Check(Directory.GetFileSystemEntries(outside).Length == 1, "Junction target changed");
        });
        Test("second_replace_failure_rolls_back_both", delegate {
            string game = Game("rollback", "original");
            InstallerEngine.BeforeReplaceForTests = delegate(int index) { if (index == 1) throw new IOException("Injected second replacement failure"); };
            Reject(delegate { InstallerEngine.Install(game, null); }, "restaurados e conferidos"); StateIs(game, "original"); NoStage(game);
            Check(!File.Exists(Path.Combine(game, PendingName)), "Successful rollback left pending journal");
            string backup = Directory.GetDirectories(Path.Combine(game, "Backup_PTBR"))[0];
            Check(File.ReadAllText(Path.Combine(backup, "backup.json")).Contains("restaurado_apos_falha"), "Rollback status missing");
        });
        Test("first_replace_failure_preserves_pair", delegate {
            string game = Game("first_failure", "original"); InstallerEngine.BeforeReplaceForTests = delegate(int index) { throw new IOException("Injected first replacement failure"); };
            Reject(delegate { InstallerEngine.Install(game, null); }, "restaurados e conferidos"); StateIs(game, "original"); NoStage(game);
        });
        Test("locked_second_archive_rollback", delegate {
            string game = Game("locked_D", "original"); FileStream locked = null;
            InstallerEngine.BeforeReplaceForTests = delegate(int index) { if (index == 1) locked = new FileStream(Path.Combine(game, "d.arc"), FileMode.Open, FileAccess.Read, FileShare.Read); };
            try { Reject(delegate { InstallerEngine.Install(game, null); }, "restaurados e conferidos"); StateIs(game, "original"); NoStage(game); }
            finally { if (locked != null) locked.Dispose(); }
        });
        Test("concurrent_installer_lock", delegate {
            string game = Game("concurrent", "original"); ManualResetEvent reached = new ManualResetEvent(false), release = new ManualResetEvent(false); Exception threadError = null;
            InstallerEngine.BeforeReplaceForTests = delegate(int index) { if (index == 0) { reached.Set(); Check(release.WaitOne(10000), "Timed out waiting for concurrency test"); } };
            Thread thread = new Thread(new ThreadStart(delegate { try { InstallerEngine.Install(game, null); } catch (Exception error) { threadError = error; } }));
            thread.Start(); Check(reached.WaitOne(10000), "Install did not reach replacement");
            try { Reject(delegate { InstallerEngine.Install(game, null); }, "Outro instalador"); }
            finally { release.Set(); thread.Join(10000); reached.Dispose(); release.Dispose(); }
            Check(!thread.IsAlive && threadError == null, "First concurrent install failed"); StateIs(game, "target");
        });
        Test("interrupted_half_install_recovers_then_installs", delegate {
            string game = Game("recover_half", "original"); InstallTransaction transaction = Pending(game, "original"); File.WriteAllBytes(Path.Combine(game, "a.arc"), Bytes("target_a"));
            InstallResult result = InstallerEngine.Install(game, null); StateIs(game, "target"); Check(!result.AlreadyInstalled, "Recovery skipped clean install");
            Check(File.ReadAllText(Path.Combine(transaction.BackupDirectory, "backup.json")).Contains("recuperado_apos_interrupcao"), "Recovered journal missing");
            Check(Hash(Path.Combine(result.BackupDirectory, "a.arc")) == HashFixture("original_a"), "Recovery backed up mixed A");
            Check(Hash(Path.Combine(result.BackupDirectory, "d.arc")) == HashFixture("original_d"), "Recovery backed up mixed D");
            Check(!File.Exists(Path.Combine(game, PendingName)), "Recovery left pending"); NoStage(game);
        });
        Test("interrupted_full_install_recovers_then_installs", delegate {
            string game = Game("recover_full", "target"); Pending(game, "original");
            InstallResult result = InstallerEngine.Install(game, null); StateIs(game, "target"); Check(!result.AlreadyInstalled, "Pending recovery skipped");
            Check(Hash(Path.Combine(result.BackupDirectory, "a.arc")) == HashFixture("original_a"), "Recovered full A not original backup");
        });
        Test("malformed_pending_journal", delegate {
            string game = Game("bad_json", "original"); File.WriteAllText(Path.Combine(game, PendingName), "{broken");
            Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original"); Check(File.Exists(Path.Combine(game, PendingName)), "Invalid journal deleted");
        });
        Test("outside_backup_path_rejected", delegate {
            string game = Game("outside_backup", "original"); InstallTransaction transaction = Pending(game, "original");
            string outside = Path.Combine(root, "outside_backup"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "marker.txt"), "untouched");
            transaction.BackupDirectory = outside; InstallerEngine.WriteJson(Path.Combine(game, PendingName), transaction);
            Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original"); Check(Directory.GetFiles(outside).Length == 1, "Outside folder modified");
        });
        Test("parent_traversal_backup_rejected", delegate {
            string game = Game("traversal_backup", "original"); InstallTransaction transaction = Pending(game, "original");
            transaction.BackupDirectory = Path.Combine(game, "Backup_PTBR", "..", "escape"); InstallerEngine.WriteJson(Path.Combine(game, PendingName), transaction);
            Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original"); Check(!Directory.Exists(Path.Combine(game, "escape")), "Traversal created directory");
        });
        Test("wrong_game_journal_rejected", delegate {
            string game = Game("wrong_game_journal", "original"); InstallTransaction transaction = Pending(game, "original"); transaction.GameDirectory = root;
            InstallerEngine.WriteJson(Path.Combine(game, PendingName), transaction); Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original");
        });
        Test("unknown_pair_journal_rejected", delegate {
            string game = Game("wrong_pair_journal", "original"); InstallTransaction transaction = Pending(game, "original"); transaction.OriginalASha256 = new string('0', 64);
            InstallerEngine.WriteJson(Path.Combine(game, PendingName), transaction); Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original");
        });
        Test("pending_backup_child_junction_rejected", delegate {
            string game = Game("pending_child_junction", "original"); InstallTransaction transaction = Pending(game, "original"); string outside = Path.Combine(root, "pending_child_outside");
            Directory.Move(transaction.BackupDirectory, outside); Junction(transaction.BackupDirectory, outside);
            Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original");
            Check(!File.Exists(Path.Combine(outside, "backup.json")), "Journal wrote outside through child junction"); Check(File.Exists(Path.Combine(game, PendingName)), "Rejected junction lost pending");
        });
        Test("pending_backup_root_junction_rejected", delegate {
            string game = Game("pending_root_junction", "original"); Pending(game, "original"); string backupRoot = Path.Combine(game, "Backup_PTBR"), outside = Path.Combine(root, "pending_root_outside");
            Directory.Move(backupRoot, outside); Junction(backupRoot, outside);
            Reject(delegate { InstallerEngine.Install(game, null); }, "pendente inválido"); StateIs(game, "original");
            Check(!File.Exists(Path.Combine(outside, "interrompida", "backup.json")), "Journal wrote outside through root junction"); Check(File.Exists(Path.Combine(game, PendingName)), "Rejected root junction lost pending");
        });
        Test("corrupted_backup_blocks_recovery_without_mutation", delegate {
            string game = Game("corrupt_backup", "original"); InstallTransaction transaction = Pending(game, "original"); File.WriteAllBytes(Path.Combine(game, "a.arc"), Bytes("target_a"));
            File.AppendAllText(Path.Combine(transaction.BackupDirectory, "d.arc"), "bad");
            Reject(delegate { InstallerEngine.Install(game, null); }, "backup não passou");
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("target_a"), "Recovery partially changed A before validating D");
            Check(Hash(Path.Combine(game, "d.arc")) == HashFixture("original_d"), "Recovery changed original D"); Check(File.Exists(Path.Combine(game, PendingName)), "Failed recovery lost journal");
        });
        Test("unknown_current_archive_blocks_recovery", delegate {
            string game = Game("unknown_recovery", "original"); Pending(game, "original"); File.AppendAllText(Path.Combine(game, "d.arc"), "unknown"); string digest = Hash(Path.Combine(game, "d.arc"));
            Reject(delegate { InstallerEngine.Install(game, null); }, "mudou desde");
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("original_a"), "Unknown recovery changed A"); Check(Hash(Path.Combine(game, "d.arc")) == digest, "Unknown recovery changed D");
        });
        Test("failed_rollback_retains_backup_and_journal", delegate {
            string game = Game("rollback_failure", "original");
            InstallerEngine.BeforeReplaceForTests = delegate(int index) { if (index == 1) { string backup = Directory.GetDirectories(Path.Combine(game, "Backup_PTBR"))[0]; File.AppendAllText(Path.Combine(backup, "d.arc"), "corrupt"); throw new IOException("Injected failure plus corrupt backup"); } };
            Reject(delegate { InstallerEngine.Install(game, null); }, "restauração automática não pôde");
            Check(File.Exists(Path.Combine(game, PendingName)), "Unfinished rollback journal missing"); Check(BackupCount(game) == 1, "Failed rollback backup missing"); NoStage(game);
            Check(Hash(Path.Combine(game, "a.arc")) == HashFixture("target_a"), "Failed rollback altered target unexpectedly"); Check(Hash(Path.Combine(game, "d.arc")) == HashFixture("original_d"), "Failed rollback altered original D");
        });
    }
    private static void CorruptTests()
    {
        string expected = variant == "truncated" ? "incompleto" : "corrompido";
        Test(variant + "_package_verification", delegate { Reject(delegate { InstallerEngine.VerifyPackage(null); }, expected); });
        Test(variant + "_install_preserves_originals", delegate {
            string game = Game(variant, "original"); Reject(delegate { InstallerEngine.Install(game, null); }, variant == "truncated" ? "incompleto" : "Falha ao conferir");
            StateIs(game, "original"); Check(BackupCount(game) == 0, "Invalid payload created backup"); Check(!File.Exists(Path.Combine(game, PendingName)), "Invalid payload created journal"); NoStage(game);
        });
    }
    public static int Main(string[] args)
    {
        variant = args.Length == 1 ? args[0] : "normal";
        string here = Path.GetDirectoryName(typeof(IntegrationTests).Assembly.Location);
        fixture = Path.Combine(here, "fixture"); root = Path.Combine(here, "execucao_" + variant + "_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        if (variant == "normal") NormalTests(); else CorruptTests();
        int failed = 0; foreach (object entry in results) if (!(bool)entry.GetType().GetProperty("passed").GetValue(entry, null)) failed++;
        InstallerEngine.WriteJson(Path.Combine(here, "resultado_" + variant + ".json"), new { variant = variant, run_directory = root, core_sha256 = Hash(Path.Combine(here, "..", "src", "InstallerEngine.cs")), fixture_manifest_sha256 = Hash(Path.Combine(fixture, "manifest.json")), total = results.Count, failed = failed, results = results });
        Console.WriteLine("RESULT " + results.Count + " tests, " + failed + " failures"); return failed == 0 ? 0 : 1;
    }
}
