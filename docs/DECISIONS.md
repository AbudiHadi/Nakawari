# Decisions

Newest at the top.

## ADR-005: The 50-series kit comes only from NVIDIA's official release (2026-10-13; narrows ADR-003)
**Context:** Dynamic MFG and the validated 6x path need DLSS-G 310.9.1 together with the full, matching Streamline 2.14.1 runtime (see the addon README's compatibility table). No game on this PC ships that combination.
**Decision:** an opt-in, per-game kit.
- **Source:** downloaded only from `github.com/NVIDIA-RTX/Streamline` release v2.14.1. HTTP range requests fetch just the production `bin/x64` runtime, about 5 MB of the 276 MB zip.
- **Check:** every file must pass WinVerifyTrust and be signed by "NVIDIA Corporation", or the whole kit is discarded.
- **Install:** all-or-nothing per game, with every original backed up through BackupVault.
- **Self-protection:** a game that crashes twice with the kit loses the kit first.
- **Isolation:** the kit's DLSS-G is never harvested as the general reference.

ADR-003 still holds for the general DLSS-G reference.
**Trade-off:** it replaces a game's Streamline set, so a game that pins its own Streamline may fail to start. That is why the kit is opt-in, asks for confirmation, and can be undone.

## ADR-004: The rename keeps class names that describe the MFG feature (2026-10-13)
The product is now Nakawari, but the classes `MfgInstaller` and `MfgConfig` keep their names. They describe the MFG Unlock feature, not the product. The namespace and all user-facing strings come from `Core/AppInfo`.

## ADR-003: DLSS-G is harvested locally, never downloaded (2026-10-13)
**Context:** the addon needs a modern `nvngx_dlssg.dll`. Downloading one from GitHub or TechPowerUp would redistribute NVIDIA binaries, which their license doesn't allow.
**Decision:** `DlssgService.Harvest` keeps the newest copy already shipped by any installed game. The user can also drop one into the DLSS-G folder.
**Trade-off:** a PC with only old games gets no newer DLL.

## ADR-002: SCSKiller is driven only through its CLI (2026-10-13)
**Context:** the owner wants everything run from one app. SCSKiller is GPL-3.0.
**Decision:** call `scskiller.exe` (`scan`, `status`, `compile`, `record install`) as a separate process, with stdin closed and ids like `steam:<appid>`. Never bundle or copy its code. Credit it as "powered by SCSKiller". If its version is unknown, fall back to an "Open SCSKiller" button.
**Trade-off:** its output is free text, so parsing is tied to the tested version (1.2.5).

## ADR-001: Velopack for install and self-update (2026-10-13)
**Context:** the owner wants an official-feeling app that updates itself. A home-made updater (renaming the running exe, swapping files) is fragile, and checking a hash from the same release only catches corruption, not tampering.
**Decision:** use Velopack (MIT), the same tool SCSKiller uses. It gives a per-user install, Start menu entry, uninstall entry, a GitHub Releases feed, verified downloads, swap-on-restart and rollback.
- `Program.Main` runs Velopack's hooks before WPF starts.
- Only the GUI checks for app updates. `--update-all` never does.
**Trade-off:** the setup and updates are unsigned, so SmartScreen warns about them (a recorded risk; signing is out of scope).
