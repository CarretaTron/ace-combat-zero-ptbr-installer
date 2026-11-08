# Ace Combat Zero — Brazilian Portuguese Translation Installer

**Translation by Carreta.** Source code for the offline installer of the Brazilian Portuguese translation of Ace Combat Zero: The Belkan War, version 010. Published to allow inspection and rebuilding of the executable submitted to Nexus Mods.

The six implementation files in `src/`, the application manifest and the artwork are the same files used in the latest **`AC0 PT-BR.exe`**, which displays the translation credit in the bottom-left corner. The distributed file's identity and original source hashes are recorded in [`provenance/release.json`](provenance/release.json).

```text
File:     AC0 PT-BR.exe
Size:     530076672 bytes
SHA-256:  3b1f91be236963001812f1e572c10a091395549d4812b47e97625901ef3f6715
```

For moderation, see [BUILDING.md](BUILDING.md) for build instructions and [REVIEW.md](REVIEW.md) for a description of the installer's behavior. The installer interface remains in Brazilian Portuguese because it is intended for users of the translation.

## What the installer does

- Lets the user select the `ACZero` directory containing `acz.exe`, `a.arc` and `d.arc`.
- Checks the ARC files using SHA-256, verifies available disk space and checks that the game is closed.
- Backs up both ARC files before replacing them and records the operation for recovery if installation fails.
- Installs the translated ARC files already embedded in the executable. It does not modify `acz.exe` or pin its version by hash.
- Displays the result in the main window and tells the user to select English (US) in the game.

The code does not access the network, download programs, create services or modify the Windows Registry. The directory picker uses the Windows API. See [REVIEW.md](REVIEW.md) for details.

## Building

Requirements: 64-bit Windows, **Windows PowerShell 5.1** and the .NET Framework 4 C# compiler, normally located at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. No NuGet packages or other external libraries are required.

This repository does not include the game, the ARC files or the 505.5 MiB installer. Reviewers who have the distributed installer can supply a copy to recover its two embedded data resources:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -InstallerExe "C:\review\AC0 PT-BR.exe"
```

The script first verifies the supplied file's SHA-256. It reads the embedded data through the .NET assembly API without calling the original installer's entry point. It then compiles the source files in this repository and verifies the new executable's embedded payload. Output: `dist\AC0 PT-BR.exe`.

Alternatively, supply the two already translated version 010 ARC files:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -PayloadDirectory "C:\review\payload-v010"
```

The required hashes are recorded in [`package/manifest.json`](package/manifest.json). The build requires the translated ARC files; unmodified game ARC files will not pass this check.

See [BUILDING.md](BUILDING.md) for tests and rebuilding details. A rebuilt executable may have a different SHA-256 because of compiler-generated identifiers and metadata. The hash above identifies the distributed file, not every subsequent build.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/InstallerEngine.cs` | Validation, hashes, backup, installation and recovery |
| `src/InstallerForm.cs` | Main window and translation credit |
| `src/InstallerDialogs.cs` | Input placeholder and invalid-directory dialog |
| `src/FolderPicker.cs` | Windows directory picker |
| `src/BufferedControls.cs` | Buffered UI controls |
| `src/Program.cs` | Startup, payload verification and previews |
| `assets/` | Installer icon, title and background artwork |
| `tests/` | Tests using synthetic data, without requiring the game |
| `package/manifest.json` | Hashes and ARC states accepted by version 010 |

## Credits and rights

Translation and distribution: **Carreta**. See [NOTICE.md](NOTICE.md). This repository contains the installer source code; it does not contain the source code or data files of Ace Combat Zero.
