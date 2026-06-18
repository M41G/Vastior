# Changelog

All notable changes to Vastior are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/); this project
aims to follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-06-17

First public‑prep release of **Vastior**, prepared and cleaned up for open
source.

### Added
- Standalone `Vastior.exe` manager (offline WinForms app): read‑only install
  status detection, install / repair / uninstall, live output, themed dark‑brass
  UI. Everything runs in‑process — no scripts, no PowerShell.
- Built‑in install/uninstall with: install manifest, path‑containment on
  restore/cleanup, backup‑before‑overwrite, **verified** save backup that
  **aborts the install if it fails**, and single‑pass uninstall (a prior Vastior
  install is not mistaken for a restorable "original").
- Version safety in the camera component: game functions are resolved at runtime;
  on an unknown build it applies nothing and the game runs vanilla.
- Open‑source docs: `README.md`, `LICENSE` (MIT for Vastior's own code),
  `NOTICE`, `SECURITY.md`, `CONTRIBUTING.md`, `.gitignore`.

### Removed
- **The heavy diagnostic / injection logger** (timestamped per‑launch logs,
  hook‑by‑hook traces, address/memory references, vectored‑exception crash
  logging) and its `LogLevel` / `SafeMode` TRACE/DEBUG flags. Vastior no longer
  writes any runtime diagnostic log and does not present as a debugging/injection
  research tool.
- No separate launcher executable is bundled; the winmm proxy auto‑loads the
  camera component when the game starts.

### Changed
- Conservative default config (`CameraMinimumZoomDistance=7`,
  `CameraMaximumZoomDistance=80`, tilt off).
- Minimal, privacy‑safe install/uninstall logging only (tool version, install
  path, files changed, backup path, errors).
- Native source restructured for readability and safety without changing
  behavior: configuration, supported‑build symbols, and camera bounds split into
  headers (`VastiorConstants.h`, `VastiorVersionInfo.h`, `VastiorCameraSettings.h`);
  the camera DLL (`VastiorCamera.cpp`) and proxy (`VastiorProxy.cpp`) broken into
  focused functions with named constants, guard clauses, install‑once /
  restore‑once byte patches, and a minimal `DllMain`. Builds clean at `-Wall`.
- Manager UI polish: removed the Preview/dry‑run checkbox (save backup is always
  on; scripts still accept `-DryRun`); realigned the Status label/badge; fixed the
  chamfered‑button corner artifacts with a shared panel backdrop; added a themed
  dark‑brass console scrollbar (no native bright bar); and replaced the folder
  picker with the modern Explorer‑style dialog. Split into reusable controls
  (`VastiorButton`, `VastiorLogConsole`, `VastiorScrollBar`, `VastiorFolderPicker`)
  with theme colours and layout sizes centralised.

### Notes
- Vastior is an independent implementation that bundles no third‑party code or
  assets; it is released under the MIT License (see `NOTICE`).
- Release binaries are unsigned (SmartScreen / antivirus may warn on first run).
  Code signing is on the roadmap.
