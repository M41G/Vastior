# Security Policy

## Reporting a vulnerability

Please report security issues **privately** first: open a GitHub *Security
Advisory* (Security → Advisories → Report a vulnerability) or email the
maintainer listed in the repository profile. Do not open a public issue for a
suspected vulnerability. We aim to acknowledge within a few days.

## Supported version

Vastior targets **Grim Dawn v1.2.1.6 (Steam, Windows)**. Fixes target the latest
release. Older releases are not maintained.

## What Vastior does and does not do (threat model)

Vastior is intentionally narrow. It **does not**:

- read, write, edit, convert, or delete character saves, the shared/transfer
  stash, or Steam Cloud save data;
- modify any file outside the Grim Dawn install folder (plus a one‑way save
  *backup* it writes to your Desktop);
- inject into any process other than `Grim Dawn.exe`;
- install services, scheduled tasks, startup/persistence entries, drivers, or
  make firewall, proxy, or registry changes;
- contact the network, send telemetry, or collect personal data;
- require administrator rights to detect install status.

It **does**:

- copy a small camera component into the Grim Dawn folder and write `Vastior.ini`;
- patch the running game's camera functions **in memory** to widen zoom (the
  camera component resolves game functions dynamically and applies nothing if
  they are absent — it never patches an unknown build);
- back up your saves and any file it overwrites before changing anything, and
  remove only what it installed on uninstall.

## Privacy of logs

The install/uninstall log (`Vastior_install.log`, in the Grim Dawn folder) is
minimal and privacy‑safe. It records only: tool version, the Grim Dawn install
path, the action taken, files installed/removed, the backup location, and simple
errors. It does **not** record memory addresses, hook internals, process memory,
save contents, Steam tokens, account data, environment variables, or recursive
directory listings.

## Unsigned binaries

Releases are currently unsigned, so Windows SmartScreen and some antivirus may
warn on first run. This is expected for a small unsigned tool. Verify release
artifacts against the published `SHA256SUMS.txt` and build from source if you
prefer.
