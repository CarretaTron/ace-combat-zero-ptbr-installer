# Notes for Nexus Mods review

This is the source for **AC0 PT-BR.exe**, the offline Portuguese (Brazil) translation installer for Ace Combat Zero: The Belkan War, version 010. Translation credit: **Carreta**.

The distributed file's SHA-256 is `3b1f91be236963001812f1e572c10a091395549d4812b47e97625901ef3f6715` (530076672 bytes). The original source/asset hashes are in [`provenance/release.json`](provenance/release.json). Build steps are in [BUILDING.md](BUILDING.md).

## Runtime behavior

- The program is a .NET Framework x64 Windows Forms executable with an `asInvoker` manifest. It does not request elevation itself.
- It has no networking code or runtime downloads, does not launch shell commands, does not create services or scheduled tasks, and does not write Windows Registry entries.
- The two translated ARC files are embedded as ordinary managed resources, along with a JSON manifest and the title/background artwork. The embedded ARC files account for most of the executable's size.
- The selected game directory must contain `acz.exe`, `a.arc` and `d.arc`. Selecting a parent that contains a valid `ACZero` child also resolves to that child. The code requires `acz.exe` to exist, but does not modify it or pin its version/hash.
- The installer checks whether the game is running, hashes the ARC files, validates the pair against known states and checks disk space/read-only attributes before creating a new installation transaction.
- It writes staging data under `.ac0-ptbr-temporario-*` inside the selected game directory, backups under `Backup_PTBR/<timestamp>/`, and a pending-transaction JSON journal. It verifies hashes and uses `File.Replace` to replace only `a.arc` and `d.arc`.
- On failure it attempts rollback from verified backups. On a later run, a pending transaction is validated before recovery. Existing backup directories are retained. If the target ARC pair is already installed, installation returns without creating another backup.
- The main interface uses `user32.dll` only for the text-box cue banner. The folder picker uses standard Windows Shell COM interfaces/`shell32.dll`.

See `InstallerEngine.cs` for the complete write/recovery paths and `Program.cs` for command-line modes. `#if TESTING` hooks in the engine are only enabled in the fixture tests, not in the release compiler command.

## Repository contents and limits

The C# implementation and installer assets are copied unchanged from the recorded release sources. `build.ps1` is a portable build wrapper prepared for this repository, replacing the original workspace-dependent wrapper. It accepts the translated ARC data separately, verifies the same manifest hashes and uses the same compiler options/resources. No game executable or ARC data is published here.

The repository makes the installer inspectable and buildable when its payload is supplied; it does not assert that the Nexus quarantine has been cleared or replace the platform's review. It also does not claim an independent malware scan or code-signing certificate.
