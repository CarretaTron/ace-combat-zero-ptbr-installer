# Build instructions

## Requirements

- 64-bit Windows with .NET Framework 4.x, Windows Forms and its 64-bit C# compiler.
- 64-bit **Windows PowerShell 5.1** (`powershell.exe`). PowerShell 7 is not supported by the build script because it uses the .NET Framework assembly APIs.
- About 2 GiB of free disk space for payload extraction, temporary output and the final executable.
- Python 3 only if running the integration tests.

No network, NuGet packages, Visual Studio or external build service is required. The commands below set the PowerShell execution policy only for that build process; they do not change the user's permanent policy.

## Build the release installer

Download/clone the repository. Obtain the distributed `AC0 PT-BR.exe` from the mod's distribution or, for moderation, the submitted file. It is intentionally not stored in this source repository. Its pinned size and SHA-256 are in `provenance/release.json`.

From the repository directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -InstallerExe "C:\review\AC0 PT-BR.exe"
```

The build script first hashes the input. It then uses `Assembly.LoadFile` and `GetManifestResourceStream` to copy the two embedded ARC data streams to `build/payload/`. It never calls the original executable's entry point. It recompiles all installer code from this repository with the .NET Framework C# compiler, embeds those data files and the repository assets, and runs **the newly compiled** executable in `--verificar-pacote` mode. This mode only reads its own resources and writes a JSON verification report; it does not install anything into a game directory.

Alternatively, place the already translated version 010 `a.arc` and `d.arc` in a directory and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -PayloadDirectory "C:\review\payload"
```

Both routes require the lengths and SHA-256 hashes from `package/manifest.json`. Unmodified game ARC files are not the translated payload. This repository does not regenerate the translation data from an original game installation; its scope is the installer code.

Output: `dist/AC0 PT-BR.exe`, plus `dist/build-report.json`. The C# input files, compiler flags, managed resources and native manifest match the released implementation. The release build used a GUID in its temporary assembly name and the compiler generates identifiers/timestamps, so a rebuilt PE file is **not expected to be byte-for-byte identical**. Inspect the code and compare IL/resources with the optional command below.

## Compare the released and rebuilt assemblies

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Compare-Assemblies.ps1 -Original "C:\review\AC0 PT-BR.exe" -Rebuilt ".\dist\AC0 PT-BR.exe"
```

This compares the C# method bodies, local-variable types, exception handlers and managed resource hashes. It does not call either assembly's entry point. This is evidence about compiled managed code and resources, not a complete byte-level proof of all PE metadata or native resource sections.

## Run integration tests without game files

```powershell
python .\tests\build_fixture.py
```

The script compiles the actual `InstallerEngine.cs` against tiny synthetic resources and runs normal, corrupted and truncated-payload cases. Tests create their own data directories under `tests/`. The synthetic `acz.exe` is a text fixture, not a game executable. These tests do not use or modify an installed game. Results are written to `tests/resultado_normal.json`, `tests/resultado_corrupt.json` and `tests/resultado_truncated.json`. Three cases create local NTFS junctions inside the test directories; an environment that forbids junction creation will report fixture-setup failures for those cases.

For directory-validation cases against the rebuilt executable:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\DirectoryValidationSmoke.ps1 -Installer ".\dist\AC0 PT-BR.exe"
```

## Preview the interface without installation

```powershell
Start-Process -FilePath ".\dist\AC0 PT-BR.exe" -ArgumentList '--previa-janela "build\preview.png"' -WindowStyle Hidden -Wait
```

Other preview modes are `--previa-sucesso`, `--previa-pasta` and `--previa-diretorio-invalido`; each takes a PNG output path. They render the installer's own controls without running an installation.
