using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Ac0PtBrInstaller
{
    public sealed class PayloadFile
    {
        public string Name { get; set; }
        public string ResourceName { get; set; }
        public string Sha256 { get; set; }
        public long Length { get; set; }
    }

    public sealed class ArchiveState
    {
        public string Name { get; set; }
        public string ASha256 { get; set; }
        public string DSha256 { get; set; }
    }

    public sealed class PackageManifest
    {
        public int FormatVersion { get; set; }
        public string Version { get; set; }
        public PayloadFile[] Files { get; set; }
        public ArchiveState[] AllowedStates { get; set; }
    }

    public sealed class InstallProgress
    {
        public int Percent { get; set; }
        public string Message { get; set; }
    }

    public sealed class InstallResult
    {
        public string GameDirectory { get; set; }
        public string BackupDirectory { get; set; }
        public bool AlreadyInstalled { get; set; }
    }

    public sealed class InstallTransaction
    {
        public int FormatVersion { get; set; }
        public string PackageVersion { get; set; }
        public string GameDirectory { get; set; }
        public string BackupDirectory { get; set; }
        public string CreatedAt { get; set; }
        public string Status { get; set; }
        public string OriginalASha256 { get; set; }
        public string OriginalDSha256 { get; set; }
        public string InstalledASha256 { get; set; }
        public string InstalledDSha256 { get; set; }
    }

    public sealed class InstallerException : Exception
    {
        public InstallerException(string message) : base(message) { }
        public InstallerException(string message, Exception inner) : base(message, inner) { }
    }

    public static class InstallerEngine
    {
        private const string PendingName = ".ac0_ptbr_instalacao_pendente.json";
        private const string BackupRootName = "Backup_PTBR";
        private const int BufferSize = 1024 * 1024;
        private static readonly Lazy<PackageManifest> Package = new Lazy<PackageManifest>(ReadManifest);
        public static PackageManifest Manifest { get { return Package.Value; } }

#if TESTING
        // These hooks are compiled only into the isolated test executable.
        public static Action<int> BeforeReplaceForTests;
        public static Func<bool> GameRunningForTests;
        public static Func<long> FreeSpaceForTests;
#endif

        public static string ResolveGameDirectory(string selection)
        {
            if (String.IsNullOrWhiteSpace(selection))
                throw new InstallerException("Escolha a pasta do Ace Combat Zero pelo botão de pasta.");
            string path;
            try { path = Path.GetFullPath(selection.Trim().Trim('"')); }
            catch (Exception error) { throw new InstallerException("O caminho da pasta não é válido.", error); }
            if (!Directory.Exists(path))
                throw new InstallerException("A pasta escolhida não existe.");
            if (!ContainsGameFiles(path))
            {
                string child = Path.Combine(path, "ACZero");
                if (ContainsGameFiles(child)) path = child;
                else throw new InstallerException("Selecione a pasta ACZero que contém acz.exe, a.arc e d.arc.");
            }
            string root = Path.GetPathRoot(path);
            return path.Length == root.Length ? root : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool ContainsGameFiles(string path)
        {
            return File.Exists(Path.Combine(path, "acz.exe")) &&
                File.Exists(Path.Combine(path, "a.arc")) && File.Exists(Path.Combine(path, "d.arc"));
        }

        public static InstallResult Install(string selection, Action<InstallProgress> progress)
        {
            string game = ResolveGameDirectory(selection);
            string lockName = "Local\\AC0_PTBR_" + HashBytes(Encoding.UTF8.GetBytes(game.ToUpperInvariant())).Substring(0, 24);
            using (Mutex mutex = new Mutex(false, lockName))
            {
                bool owned;
                try { owned = mutex.WaitOne(0, false); }
                catch (AbandonedMutexException) { owned = true; }
                if (!owned) throw new InstallerException("Outro instalador já está trabalhando nesta pasta. Aguarde a conclusão.");
                try { return InstallLocked(game, progress); }
                catch (UnauthorizedAccessException error)
                {
                    throw new InstallerException("Sem permissão para gravar nesta pasta. Feche o instalador e execute-o como administrador.", error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static InstallResult InstallLocked(string game, Action<InstallProgress> progress)
        {
            EnsureGameClosed();
            Report(progress, 1, "Conferindo a pasta e os pacotes do jogo...");

            RecoverPending(game, progress);
            string a = Path.Combine(game, "a.arc"), d = Path.Combine(game, "d.arc");
            string oldA = HashFile(a), oldD = HashFile(d);
            PayloadFile newA = FindPayload("a.arc"), newD = FindPayload("d.arc");
            if (EqualHash(oldA, newA.Sha256) && EqualHash(oldD, newD.Sha256))
            {
                Report(progress, 100, "Esta versão da tradução já está instalada.");
                return new InstallResult { GameDirectory = game, AlreadyInstalled = true };
            }
            if (!IsAllowedPair(oldA, oldD))
                throw new InstallerException("Os pacotes desta pasta não correspondem a uma versão compatível. Os arquivos podem ser de outra edição do jogo ou ter outras modificações.");
            foreach (string target in new string[] { a, d })
                if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
                    throw new InstallerException("O arquivo " + Path.GetFileName(target) + " está marcado como somente leitura. Remova essa marca nas propriedades do arquivo e tente novamente.");
            CheckFreeSpace(game, new FileInfo(a).Length + new FileInfo(d).Length + newA.Length + newD.Length + 64L * 1024 * 1024);

            string stage = Path.Combine(game, ".ac0-ptbr-temporario-" + Guid.NewGuid().ToString("N"));
            string backupRoot = Path.Combine(game, BackupRootName);
            RejectDirectoryLink(backupRoot);
            string backup = Path.Combine(backupRoot, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string pending = Path.Combine(game, PendingName);
            bool commitStarted = false;
            InstallTransaction transaction = new InstallTransaction {
                FormatVersion = 1, PackageVersion = Manifest.Version, GameDirectory = game,
                BackupDirectory = backup, CreatedAt = DateTime.Now.ToString("o"), Status = "preparando",
                OriginalASha256 = oldA, OriginalDSha256 = oldD,
                InstalledASha256 = newA.Sha256, InstalledDSha256 = newD.Sha256
            };
            try
            {
                Directory.CreateDirectory(stage);
                RejectDirectoryLink(stage);
                int number = 0;
                foreach (PayloadFile file in Manifest.Files)
                {
                    int begin = number == 0 ? 5 : 38, end = number == 0 ? 38 : 48;
                    using (Stream input = OpenPayload(file))
                        CopyVerified(input, Path.Combine(stage, file.Name), file.Sha256, file.Length,
                            progress, begin, end, "Preparando a tradução: " + file.Name);
                    number++;
                }
                EnsureGameClosed();
                RejectDirectoryLink(backupRoot);
                Directory.CreateDirectory(backup);
                RejectDirectoryLink(backup);
                WriteJson(Path.Combine(backup, "backup.json"), transaction);
                Report(progress, 50, "Guardando os arquivos atuais no backup...");
                CopyVerifiedFile(a, Path.Combine(backup, "a.arc"), oldA, progress, 50, 76, "Salvando o backup: a.arc");
                CopyVerifiedFile(d, Path.Combine(backup, "d.arc"), oldD, progress, 76, 80, "Salvando o backup: d.arc");
                File.WriteAllText(Path.Combine(backup, "LEIA-ME.txt"),
                    "Backup da instalação da tradução PT-BR " + Manifest.Version + ".\r\n\r\n" +
                    "Para restaurar: feche o jogo e copie a.arc e d.arc desta pasta para:\r\n" + game +
                    "\r\nSubstitua os dois arquivos. Guarde este backup até conferir o jogo.\r\n", Encoding.UTF8);
                EnsureGameClosed();
                if (!EqualHash(HashFile(a), oldA) || !EqualHash(HashFile(d), oldD))
                    throw new InstallerException("Os arquivos do jogo mudaram durante a preparação. Feche os outros programas e tente novamente.");

                transaction.Status = "instalando";
                WriteJson(pending, transaction);
                commitStarted = true;
                number = 0;
                foreach (PayloadFile file in Manifest.Files)
                {
                    EnsureGameClosed();
#if TESTING
                    if (BeforeReplaceForTests != null) BeforeReplaceForTests(number);
#endif
                    Report(progress, 82 + number * 6, "Instalando " + file.Name + "...");
                    string target = Path.Combine(game, file.Name);
                    string expectedOld = file.Name == "a.arc" ? oldA : oldD;
                    if (!EqualHash(HashFile(target), expectedOld))
                        throw new InstallerException("Um arquivo do jogo foi alterado durante a instalação.");
                    File.Replace(Path.Combine(stage, file.Name), target, null, true);
                    if (!EqualHash(HashFile(target), file.Sha256))
                        throw new InstallerException("A conferência do arquivo instalado falhou: " + file.Name);
                    number++;
                }
                if (!EqualHash(HashFile(a), newA.Sha256) || !EqualHash(HashFile(d), newD.Sha256))
                    throw new InstallerException("A conferência final dos pacotes falhou.");
                transaction.Status = "instalado";
                WriteJson(Path.Combine(backup, "backup.json"), transaction);
                File.Delete(pending);
                Report(progress, 100, "Tradução instalada e arquivos conferidos.");
                return new InstallResult { GameDirectory = game, BackupDirectory = backup, AlreadyInstalled = false };
            }
            catch (Exception error)
            {
                if (commitStarted)
                {
                    try
                    {
                        Report(progress, 95, "Restaurando os arquivos anteriores...");
                        RestoreTransaction(game, transaction);
                        File.Delete(pending);
                        transaction.Status = "restaurado_apos_falha";
                        WriteJson(Path.Combine(backup, "backup.json"), transaction);
                    }
                    catch (Exception restoreError)
                    {
                        throw new InstallerException("A instalação falhou e a restauração automática não pôde ser concluída. Feche o jogo e restaure a.arc e d.arc do backup:\r\n" + backup +
                            "\r\nMotivo: " + restoreError.Message, error);
                    }
                    throw new InstallerException("A instalação falhou. Os dois arquivos anteriores foram restaurados e conferidos.\r\n" + error.Message, error);
                }
                if (error is UnauthorizedAccessException) throw;
                throw new InstallerException("Não foi possível concluir a preparação. Os arquivos do jogo foram preservados.\r\n" + error.Message, error);
            }
            finally { CleanStage(game, stage); }
        }

        private static void RecoverPending(string game, Action<InstallProgress> progress)
        {
            string pending = Path.Combine(game, PendingName);
            if (!File.Exists(pending)) return;
            InstallTransaction transaction;
            try
            {
                transaction = new JavaScriptSerializer().Deserialize<InstallTransaction>(File.ReadAllText(pending, Encoding.UTF8));
                ValidateTransaction(game, transaction);
            }
            catch (Exception error) { throw new InstallerException("Existe um registro de instalação pendente inválido nesta pasta. Consulte o backup em Backup_PTBR antes de continuar.", error); }
            Report(progress, 2, "Recuperando uma instalação anterior interrompida...");
            RestoreTransaction(game, transaction);
            File.Delete(pending);
            transaction.Status = "recuperado_apos_interrupcao";
            WriteJson(Path.Combine(transaction.BackupDirectory, "backup.json"), transaction);
        }

        private static void ValidateTransaction(string game, InstallTransaction transaction)
        {
            if (transaction == null || transaction.FormatVersion != 1 ||
                !String.Equals(Path.GetFullPath(transaction.GameDirectory), game, StringComparison.OrdinalIgnoreCase) ||
                !IsAllowedPair(transaction.OriginalASha256, transaction.OriginalDSha256) ||
                !IsAllowedPair(transaction.InstalledASha256, transaction.InstalledDSha256))
                throw new InstallerException("O registro pendente não corresponde a esta pasta ou versão.");
            string backupRoot = Path.Combine(game, BackupRootName) + Path.DirectorySeparatorChar;
            string backup = Path.GetFullPath(transaction.BackupDirectory);
            if (!backup.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(Path.GetDirectoryName(backup), backupRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InstallerException("O caminho do backup pendente não é válido.");
            RejectDirectoryLink(backupRoot.TrimEnd(Path.DirectorySeparatorChar));
            RejectDirectoryLink(backup);
        }

        private static void RejectDirectoryLink(string path)
        {
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InstallerException("A pasta de backup ou preparação não pode ser um link para outra pasta: " + path);
        }

        private static void RestoreTransaction(string game, InstallTransaction transaction)
        {
            EnsureGameClosed();
            ValidateTransaction(game, transaction);
            foreach (string name in new string[] { "a.arc", "d.arc" })
            {
                string old = name == "a.arc" ? transaction.OriginalASha256 : transaction.OriginalDSha256;
                string installed = name == "a.arc" ? transaction.InstalledASha256 : transaction.InstalledDSha256;
                string backup = Path.Combine(transaction.BackupDirectory, name);
                if (!File.Exists(backup) || !EqualHash(HashFile(backup), old))
                    throw new InstallerException("O backup não passou na conferência: " + name);
                string actual = HashFile(Path.Combine(game, name));
                if (!EqualHash(actual, old) && !EqualHash(actual, installed))
                    throw new InstallerException("O arquivo " + name + " mudou desde a instalação. A cópia de backup foi preservada.");
            }
            foreach (string name in new string[] { "a.arc", "d.arc" })
            {
                string old = name == "a.arc" ? transaction.OriginalASha256 : transaction.OriginalDSha256;
                string target = Path.Combine(game, name);
                if (EqualHash(HashFile(target), old)) continue;
                EnsureGameClosed();
                string temporary = Path.Combine(game, ".ac0-ptbr-restaurar-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    CopyVerifiedFile(Path.Combine(transaction.BackupDirectory, name), temporary, old, null, 0, 0, "");
                    File.Replace(temporary, target, null, true);
                    if (!EqualHash(HashFile(target), old)) throw new InstallerException("Falha ao conferir a restauração de " + name);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            if (!EqualHash(HashFile(Path.Combine(game, "a.arc")), transaction.OriginalASha256) ||
                !EqualHash(HashFile(Path.Combine(game, "d.arc")), transaction.OriginalDSha256))
                throw new InstallerException("A restauração dos dois pacotes não passou na conferência final.");
        }

        public static Dictionary<string, object> VerifyPackage(Action<InstallProgress> progress)
        {
            List<object> files = new List<object>();
            foreach (PayloadFile file in Manifest.Files)
            {
                Report(progress, 0, "Conferindo o pacote incluído: " + file.Name);
                using (Stream stream = OpenPayload(file))
                {
                    string digest = HashStream(stream);
                    if (!EqualHash(digest, file.Sha256)) throw new InstallerException("O pacote incluído está corrompido: " + file.Name);
                    files.Add(new { name = file.Name, length = file.Length, sha256 = digest });
                }
            }
            return new Dictionary<string, object> { { "ok", true }, { "version", Manifest.Version }, { "files", files } };
        }

        private static PackageManifest ReadManifest()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AC0_PTBR.manifest.json"))
            {
                if (stream == null) throw new InstallerException("O manifesto incluído no instalador não foi encontrado.");
                PackageManifest manifest;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    manifest = new JavaScriptSerializer().Deserialize<PackageManifest>(reader.ReadToEnd());
                if (manifest == null || manifest.FormatVersion != 1 || manifest.Files == null || manifest.Files.Length != 2 ||
                    manifest.AllowedStates == null || manifest.AllowedStates.Length == 0)
                    throw new InstallerException("O manifesto incluído no instalador é inválido.");
                if (manifest.Files[0].Name != "a.arc" || manifest.Files[1].Name != "d.arc")
                    throw new InstallerException("O instalador deve conter os dois pacotes esperados.");
                foreach (PayloadFile file in manifest.Files)
                    if (file.ResourceName != "AC0_PTBR." + file.Name || !ValidHash(file.Sha256) || file.Length < 1)
                        throw new InstallerException("O manifesto de um pacote incluído é inválido.");
                foreach (ArchiveState state in manifest.AllowedStates)
                    if (!ValidHash(state.ASha256) || !ValidHash(state.DSha256))
                        throw new InstallerException("O manifesto de compatibilidade é inválido.");
                return manifest;
            }
        }

        private static PayloadFile FindPayload(string name)
        {
            foreach (PayloadFile file in Manifest.Files) if (file.Name == name) return file;
            throw new InstallerException("O instalador não contém " + name);
        }

        private static Stream OpenPayload(PayloadFile file)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file.ResourceName);
            if (stream == null || stream.Length != file.Length)
            {
                if (stream != null) stream.Dispose();
                throw new InstallerException("O pacote incluído está incompleto: " + file.Name);
            }
            return stream;
        }

        private static void CopyVerifiedFile(string source, string destination, string hash,
            Action<InstallProgress> progress, int begin, int end, string message)
        {
            using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                CopyVerified(input, destination, hash, input.Length, progress, begin, end, message);
        }

        private static void CopyVerified(Stream input, string destination, string expectedHash, long expectedLength,
            Action<InstallProgress> progress, int begin, int end, string message)
        {
            byte[] buffer = new byte[BufferSize];
            long total = 0;
            int lastPercent = -1;
            using (SHA256 hash = SHA256.Create())
            using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, count);
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                    total += count;
                    if (total > expectedLength) throw new InstallerException("Um pacote excedeu o tamanho esperado.");
                    int percent = begin + (int)((end - begin) * total / Math.Max(1L, expectedLength));
                    if (percent != lastPercent) { Report(progress, percent, message); lastPercent = percent; }
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                output.Flush(true);
                if (total != expectedLength || !EqualHash(ToHex(hash.Hash), expectedHash))
                    throw new InstallerException("Falha ao conferir a cópia: " + Path.GetFileName(destination));
            }
            if (!EqualHash(HashFile(destination), expectedHash))
                throw new InstallerException("A cópia gravada no disco diverge do pacote esperado.");
        }

        private static bool IsAllowedPair(string a, string d)
        {
            foreach (ArchiveState state in Manifest.AllowedStates)
                if (EqualHash(a, state.ASha256) && EqualHash(d, state.DSha256)) return true;
            return false;
        }

        private static void EnsureGameClosed()
        {
#if TESTING
            if (GameRunningForTests != null)
            {
                if (GameRunningForTests()) throw new InstallerException("Feche o jogo antes de instalar a tradução.");
                return;
            }
#endif
            Process[] processes = Process.GetProcessesByName("acz");
            try { if (processes.Length != 0) throw new InstallerException("Feche o jogo antes de instalar a tradução."); }
            finally { foreach (Process process in processes) process.Dispose(); }
        }

        private static void CheckFreeSpace(string game, long required)
        {
            long free;
#if TESTING
            if (FreeSpaceForTests != null) free = FreeSpaceForTests();
            else
#endif
                free = new DriveInfo(Path.GetPathRoot(game)).AvailableFreeSpace;
            if (free < required)
                throw new InstallerException("Não há espaço livre suficiente para a tradução e o backup. São necessários cerca de " +
                    ((required + 1024 * 1024 - 1) / (1024 * 1024)).ToString() + " MB livres nesta unidade.");
        }

        private static void CleanStage(string game, string stage)
        {
            // Delete only our two fixed files from the directory we created.
            // Never recursively remove a selected or computed game directory.
            try
            {
                if (!String.Equals(Path.GetDirectoryName(stage), game, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(stage).StartsWith(".ac0-ptbr-temporario-", StringComparison.Ordinal) || !Directory.Exists(stage)) return;
                foreach (string name in new string[] { "a.arc", "d.arc" })
                {
                    string path = Path.Combine(stage, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                if (Directory.GetFileSystemEntries(stage).Length == 0) Directory.Delete(stage, false);
            }
            catch { /* A leftover temporary folder must not hide the install result. */ }
        }

        public static void WriteJson(string path, object data)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(data));
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string HashFile(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return HashStream(stream);
        }
        private static string HashStream(Stream stream)
        { using (SHA256 hash = SHA256.Create()) return ToHex(hash.ComputeHash(stream)); }
        private static string HashBytes(byte[] bytes)
        { using (SHA256 hash = SHA256.Create()) return ToHex(hash.ComputeHash(bytes)); }
        private static string ToHex(byte[] bytes)
        { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        private static bool EqualHash(string a, string b)
        { return !String.IsNullOrEmpty(a) && String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        private static bool ValidHash(string value)
        { return value != null && Regex.IsMatch(value, "^[0-9a-fA-F]{64}$"); }
        private static void Report(Action<InstallProgress> progress, int percent, string message)
        { if (progress != null) progress(new InstallProgress { Percent = percent, Message = message }); }
    }
}
