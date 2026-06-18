# Vastior

**A small, safe camera‑zoom mod manager for Grim Dawn.**

Vastior lets you zoom the camera out (and in) farther than the stock Grim Dawn
limit, with an optional gentle downward tilt when zoomed in close. It installs a
tiny camera component into your Grim Dawn folder that loads automatically when
you launch the game — no separate launcher, no manual steps.

> **Unofficial community tool.** Not affiliated with or endorsed by Crate
> Entertainment, Grim Dawn, or Valve/Steam.

---

## What Vastior is

- A standalone Windows app (`Vastior.exe`) that **installs / repairs / uninstalls**
  the camera mod and shows whether it's currently installed.
- A camera component (`Vastior.dll`) that widens the zoom range at runtime, plus
  a `winmm.dll` proxy that auto‑loads it. Everything is driven by a simple
  `Vastior.ini`.

## What Vastior is **not**

- ❌ Not a save editor, stash editor, character editor, or cheat tool.
- ❌ It does **not** read, write, edit, convert, or delete your characters,
  shared stash, saves, or Steam Cloud data.
- ❌ No persistence, services, scheduled tasks, startup entries, firewall/proxy
  changes, or registry changes.
- ❌ No network activity. No telemetry. No injection into anything but Grim Dawn.

---

## Safety

- **Your saves are never touched.** The only save interaction is a precautionary
  one‑way **backup to your Desktop** before installing — and the install
  **aborts** if that backup can't be verified.
- **Reversible.** Every install is tracked in a manifest; uninstall removes only
  what Vastior installed and **restores anything it replaced**, leaving the game
  stock. Save backups are never deleted.
- **No admin needed for status.** Detecting installed/not‑installed is read‑only.
  Installing only needs write access to the Grim Dawn folder (see Troubleshooting).
- **Degrades safely.** The camera component resolves all game functions at
  runtime; if a future Grim Dawn build changes them, Vastior **applies nothing
  and the game runs normally** instead of risking a crash.

---

## Install

1. Download the latest release `.zip` and extract it. Keep `Vastior.exe` and the
   `files\` folder together.
2. Double‑click **`Vastior.exe`**.
3. Confirm the detected **Game folder** (or click **Browse**).
4. Click **Install / Repair** and confirm. Vastior backs up your saves first,
   installs the camera files, and the status flips to **INSTALLED**.
5. Launch Grim Dawn normally (any launch option works: x86 DX11, x64 DX11, or
   x86 Legacy DX9). Use the **mouse wheel** to zoom.

### Tweaking
Edit `Vastior.ini` in your Grim Dawn folder, save, then fully close and relaunch
the game. All values are range‑checked and clamped to safe limits.

**About the default zoom‑out (`CameraMaximumZoomDistance = 60`):** 60 is the
farthest you can zoom while Grim Dawn still draws ground‑item labels. You can set
it higher (up to 500) for more zoom, but past ~60 the game stops rendering those
loot labels — that's a Grim Dawn limit, not a Vastior setting. Lower it again to
get the labels back.

## Uninstall

Open `Vastior.exe` and click **Uninstall**. It removes only what it installed
and restores the game to stock. Your save backups on the Desktop are left
untouched.

## Restore

If you ever need your saves, they're in the timestamped folder on your Desktop:
`Grim Dawn Backups\GD_Save_Backup_<date>\`. **If you restore Steam Cloud saves,
disable Steam Cloud for Grim Dawn first**, or Steam may overwrite them on sync.

---

## Compatibility

- **Target: Grim Dawn v1.2.1.6 (Steam).**
- Other/unknown builds are treated cautiously: if the camera functions Vastior
  expects aren't present, it does nothing and the game runs vanilla. After a
  Grim Dawn update, re‑verify that zoom still works; if it stops, the build moved
  past this version — check for a Vastior update.

## How install / uninstall works

Everything runs **inside `Vastior.exe`** — there are no scripts and no
PowerShell. Install copies the camera files into the Grim Dawn folder (after a
verified save backup) and writes a small text manifest (`Vastior_install.txt`);
uninstall reads that manifest to remove exactly what it installed and restore
anything it replaced. Nothing keeps running afterwards.

## Antivirus & SmartScreen

**Vastior is currently unsigned, and some antivirus engines (and VirusTotal) may
flag it as a false positive.** Here is the honest situation so you can decide for
yourself:

- **Why it happens.** Vastior is a small, *unsigned* .NET program that copies a
  few files and adjusts the game's camera in memory. That shape resembles what
  some machine‑learning/heuristic engines associate with .NET malware, so they
  guess "malicious" even though the behaviour is benign. The camera component
  (`Vastior.dll` / the `winmm.dll` proxy) is the most likely to be flagged
  because patching the game's own camera in memory looks like code injection.
- **What Vastior does *not* do** (verifiable in the source): no network
  connections, no telemetry, no auto‑updater, no registry writes, no persistence
  /services/scheduled tasks, no access to your saves, characters, shared stash,
  or Steam Cloud. It only reads the Steam registry key to find your game and
  writes camera files into the Grim Dawn folder.
- **How to verify before trusting it.** The full source is in this repository and
  the release is reproducible. Check the file properties (Details tab) show
  **Vastior / version 1.0.0.0**, and confirm the SHA‑256 hashes against
  `SHA256SUMS.txt` on the release page.
- **If SmartScreen warns** (*"Windows protected your PC"*): click **More info →
  Run anyway** if you trust this build.
- **If your antivirus blocks a file:** allow it, or simply don't install — nothing
  is left behind. You can also report the false positive to your vendor.

Code signing (via the SignPath Foundation open‑source program) is planned, which
removes the "unknown publisher" warning and clears most of these flags.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Vastior.exe` won't find the game | Click **Browse** and select your `…\steamapps\common\Grim Dawn` folder. |
| Install fails: "folder is not writable" | Your Grim Dawn folder needs write access. Right‑click `Vastior.exe` → **Run as administrator** for the install, or grant your user **Modify** on the folder. Status detection never needs admin. |
| Zoom doesn't work after install | Make sure you fully closed and relaunched the game. After a Grim Dawn update, check for a Vastior update. |
| "Missing files folder" | Keep `Vastior.exe` next to its `files\` folder. |
| Far edges look hazy at max zoom | Raise `CameraFarClipDistance` in `Vastior.ini` (keep it ≥ `CameraMaximumZoomDistance`). |
| Item/loot labels vanish when zoomed far out | A Grim Dawn limit past ~60 — it stops drawing ground‑item labels. Keep `CameraMaximumZoomDistance` at `60` (default) for labels, or raise it for more zoom without them. |

---

## Building from source

You need a C# compiler. Any of these works:

- **.NET SDK** (recommended): `dotnet build` (see `CONTRIBUTING.md` for a minimal
  project file), or
- **Visual Studio / Build Tools** (`csc.exe`), or
- the **in‑box .NET Framework compiler** present on most Windows installs:
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

Compile the manager:
```
csc /target:winexe /win32icon:assets\Vastior.ico /out:Vastior.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs
```
The native camera component (`src\VastiorCamera.cpp`) and proxy
(`src\VastiorProxy.cpp`), with their shared headers, are built with a 32‑bit
**and** a 64‑bit MinGW‑w64 toolchain — see `CONTRIBUTING.md` for exact commands.
Prebuilt binaries are included in releases.

---

## Attribution & license

- Vastior's code (the manager app, the camera component, the installer logic,
  and this documentation) is released under the **MIT License** — see
  `LICENSE`.
- Vastior is an **independent implementation** that adjusts Grim Dawn's own
  in‑game camera and bundles no third‑party code, binaries, or assets — see
  `NOTICE`.
- "Grim Dawn" and related marks belong to **Crate Entertainment**. Vastior is an
  unofficial fan tool.

Report security concerns via `SECURITY.md`.
