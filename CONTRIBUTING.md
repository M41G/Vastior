# Contributing to Vastior

Thanks for your interest! Vastior is deliberately small and safety‑focused.
Please keep changes in that spirit.

## Hard rules (non‑negotiable)

A change will be rejected if it adds any of:

- save editing, stash editing, character editing, or cheats;
- writing/reading outside the Grim Dawn folder (except the documented Desktop
  save *backup*);
- injection into any process other than `Grim Dawn.exe`;
- persistence, services, scheduled tasks, startup entries, drivers;
- firewall, proxy, registry, or machine/user execution‑policy changes;
- network activity, telemetry, or data collection;
- requiring admin rights for read‑only status detection;
- stealth/obfuscation, or a heavy runtime diagnostic/injection logger.

The install/uninstall scripts are the **source of truth** for file operations.
Keep them hardened: dry‑run, manifest, path‑containment, backup‑before‑overwrite,
verified save backup, and "remove only what we installed."

## Building

### Manager app (`Vastior.exe`)
Any C# compiler works. Pick one:

- **.NET SDK** (`dotnet`): create a minimal `Vastior.csproj`
  (`<OutputType>WinExe</OutputType>`, `net48` or later, references to
  `System.Windows.Forms` / `System.Drawing`) and `dotnet build -c Release`.
- **Visual Studio Build Tools / Roslyn `csc`**, or
- **In‑box .NET Framework compiler** (present on most Windows installs):
  ```
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe ^
    /win32icon:assets\Vastior.ico /out:Vastior.exe /r:System.dll /r:System.Drawing.dll ^
    /r:System.Windows.Forms.dll src\*.cs
  ```
  > We do not assume every machine has a compiler; the README points contributors
  > at the SDK first and ships a prebuilt `Vastior.exe` in releases.

  The manager is split into focused files compiled together (`src\*.cs`):
  `VastiorTheme` (palette/fonts), `VastiorLayout` (dimensions), `VastiorButton`,
  `VastiorTitleButton`, `VastiorScrollBar`, `VastiorLogConsole`,
  `VastiorFolderPicker`, `VastiorInstaller` (in‑process install/uninstall), and
  `Vastior` (the form). Builds clean at `/warn:4`, and references only in-box
  .NET Framework assemblies (no NuGet / third-party packages). The install
  manifest is a plain text file, so no JSON/serialization library is needed.

### Native camera component & proxy
Built with **MinGW‑w64 GCC** (32‑bit and 64‑bit). Reference: w64devkit 2.8.0
(GCC 16.x). Pin your toolchain in the PR description.

The camera component is `src/VastiorCamera.cpp`; the proxy is
`src/VastiorProxy.cpp`. Both include the shared headers `VastiorConstants.h`,
`VastiorVersionInfo.h`, and `VastiorCameraSettings.h` (no separate compile step).
Build clean at `-Wall` (no warnings expected). `-frandom-seed` + `--no-insert-timestamp`
make the output reproducible (two builds of the same source are byte-identical),
so the shipped binaries can be independently rebuilt and compared:

```
# 32-bit (i686 toolchain)
gcc -std=c++17 -Os -Wall -fno-exceptions -fno-rtti -s -shared -frandom-seed=vastior \
    -Wl,--subsystem,windows,--kill-at,--no-insert-timestamp \
    -o files/Vastior.dll src/VastiorCamera.cpp
gcc -std=c++17 -Os -Wall -fno-exceptions -fno-rtti -s -shared -frandom-seed=vastior \
    -Wl,--subsystem,windows,--no-insert-timestamp \
    -o files/winmm.dll src/VastiorProxy.cpp src/winmm_fwd.def

# 64-bit (x86_64 toolchain)
gcc -std=c++17 -Os -Wall -fno-exceptions -fno-rtti -s -shared -frandom-seed=vastior \
    -Wl,--subsystem,windows,--kill-at,--no-insert-timestamp \
    -o files/x64/Vastior.dll src/VastiorCamera.cpp
gcc -std=c++17 -Os -Wall -fno-exceptions -fno-rtti -s -shared -frandom-seed=vastior \
    -Wl,--subsystem,windows,--no-insert-timestamp \
    -o files/x64/winmm.dll src/VastiorProxy.cpp src/winmm64_fwd.def
```

**Hardening notes (verify before release):**
* **DEP + ASLR** are on by default with this toolchain — confirm with
  `objdump -p <file> | grep DllCharacteristics` (expect `0140` x86 / `0160` x64 =
  `DYNAMIC_BASE`+`NX_COMPAT`(+`HIGH_ENTROPY_VA`)), or run **BinSkim**.
* **CFG** (`GUARD_CF`) is not emitted by MinGW or the in-box `csc` (MSVC-only); we
  accept its absence for a tool this small.
* **Stack protector is intentionally omitted.** `-fstack-protector*` pulls an
  `ADVAPI32` import (canary seeded from `RtlGenRandom`) and that crypto+`WriteProcessMemory`
  combo trips AV heuristics; the native code has no unbounded stack buffers, so DEP/ASLR
  already cover the realistic surface. Keep imports to `KERNEL32` + `msvcrt`.
* **Code-sign** all binaries before release (`tools/sign.ps1`); unsigned + winmm-proxy
  is the main AV false-positive driver.
`winmm_fwd.def` / `winmm64_fwd.def` are forwarder definitions built from the
system `winmm.dll` export table (one line per export: `Name=winmm_orig.Name`).
The camera DLL must import only `KERNEL32.dll` and `msvcrt.dll`; the proxy must
export the full forwarder set (verify with `objdump -p`).

## Testing

Install and uninstall are handled in‑process by `VastiorInstaller`. Validate
against a throwaway sandbox (a folder with dummy `Grim Dawn.exe`, `engine.dll`,
`game.dll`, and `x64\Grim Dawn.exe`) by calling
`VastiorInstaller.Install(sandbox, releaseDir, false, log)` then
`VastiorInstaller.Uninstall(sandbox, log)` from a small test `Main` (pass
`backupSaves: false` so real saves are never touched).

Confirm: manifest written, exact‑match single‑pass uninstall, a pre‑existing
`winmm.dll` is backed up then restored, **zero leftovers** after repeated
cycles, game files left intact, and that a real `…\My Games\Grim Dawn\save`
folder is never read or written.

## Style

- C#: C# 5–compatible (the in‑box `csc` is used by some contributors), 4‑space
  indent, no LINQ in hot paths, no unsafe code.
- Install/uninstall (C#): validate the target is a Grim Dawn folder, contain
  every manifest path to it, back up before overwriting, and remove only what
  the manifest records.
- C/C++: no CRT‑heavy code in `DllMain`; validate pointers; restore patches on
  unload; no diagnostic logging in release.

Open an issue before large changes. PRs should describe the safety impact.
