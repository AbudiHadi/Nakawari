# Known issues

Format: **KI-NNN (title)**: symptom, then cause, then fix, then a prevention rule.

## KI-002: "Close the game first" with no game running (fixed 2026-10-13)
- **Symptom:** Crisol, Luto and Hogwarts Legacy showed "Close the game first" while nothing was running.
- **Cause:** `SystemInfo.IsRunning` matched processes by EXE **name** only. SCSKiller warms shaders by running same-named copies of games from `%LOCALAPPDATA%\SCSKiller\games\<id>\work\stage-*\`, and those matched.
- **Fix:** compare each process's full image path (QueryFullProcessImageName) with the game's EXE. An unreadable path still counts as running, to stay safe.
- **Prevention rule:** never identify a game process by name alone. Always compare the full path.

## KI-001: Disable after "Manage" deleted a hand-installed addon (fixed 2026-10-13)
- **Symptom:** the user clicked Manage, then Disable, on CONTROL Resonant. The addon they had installed by hand was deleted, and `mfgunlock-version.txt` was left saying 1.4.3. Their own updater script then believed it was up to date and never restored the file, so MFG was off.
- **Cause:** taking over an install tracked the existing files but never backed them up.
- **Fix:** `BackupVault.Preserve` now runs before any untracked file is overwritten (the addon, the legacy version file, DLSS-G).
- **Prevention rule:** a ledger entry for a file that existed before Nakawari touched it must carry a `BackupPath`.
