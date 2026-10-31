# Nakawari (中割り)

**DLSS Multi Frame Generation (3x/4x) on RTX 40 cards, one click per game.** A Windows app by [AbudiHadi](https://github.com/AbudiHadi) that installs the community MFG unlock into your games safely, keeps it updated, checks that it works, and puts every folder back exactly as it was when you turn it off. It is built to run alongside [SCSKiller](https://github.com/BlueHeisenberg/SCSKiller)'s shader cache.

*Nakawari* is the Japanese animation term for the in-between drawings placed between key frames. That is what frame generation does.

Free and open source under the [MIT License](LICENSE). Use it, fork it, ship it.

> **Note on the art.** The published app uses the author's own watercolor paintings. Those are not part of this repository. This tree ships simple placeholder graphics instead, so it builds and runs out of the box. See [Art](#art) below.

## Download

1. Get `Nakawari-win-Setup.exe` from the [latest release](https://github.com/AbudiHadi/Nakawari/releases/latest).
2. Run it. It installs per user, adds a Start menu entry, and updates itself from this repository's releases.
3. Windows SmartScreen may warn because the installer is not code-signed. Choose **More info > Run anyway**, or build it yourself from source (see [Build](#build)).

## Use it

1. Open Nakawari. It scans Steam, Epic, GOG and any folders you add, and lists the games that ship DLSS Frame Generation.
2. Click **Enable** on a game.
3. In the game, pick the multiplier from its own frame-generation menu. If the game only has an on/off switch, click **Multiplier** in Nakawari to force 2x to 6x.
4. Click **Update all** whenever you like, or tick **Auto-update at sign-in**.

**Disable** puts the folder back byte for byte. Every file Nakawari places or replaces is recorded in a ledger, and every file it overwrites is backed up first.

### What Enable does

1. **Checks first, and refuses rather than guesses.** It stops if the game has online anti-cheat (EAC, BattlEye, EA AC, Vanguard, ACE and others), has no DLSS Frame Generation, is running, or already has another mod using `dxgi.dll`.
2. **ReShade (add-on build).** If the game has no ReShade yet, Nakawari downloads the official installer from reshade.me once, takes `ReShade64.dll` out of it, and places it as `dxgi.dll`.
3. **MFG Unlock addon.** It downloads the latest GitHub release, checks the file size, and copies it next to the game EXE.
4. **DLSS-G.** If the game's `nvngx_dlssg.dll` is older than the newest NVIDIA-signed copy found in your other games, it is replaced and the original is backed up. You can turn this off.
5. **Settings.** It writes the addon's section into `ReShade.ini`: enabled, your multiplier, and the HDR fix for the games that need it (from `profiles.json`).

### The doctor

After you play, Nakawari reads the addon's status file or ReShade's log and tells you whether MFG really ran, the real maximum multiplier your driver allowed, and whether a game patch undid the install. It repairs missing files, follows games you moved, and rolls back after repeated crashes.

### The 50-series kit (optional, per game)

Dynamic MFG and the full 6x path need NVIDIA's Streamline 2.14.1 runtime with DLSS-G 310.9.1. On request, Nakawari fetches just the production runtime from NVIDIA's [official release](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.1), accepts it only if every file carries a valid NVIDIA signature, swaps it into one game with full backups, and removes it again on request or after two crashes.

### SCSKiller

Nakawari and SCSKiller use different files, so they do not conflict. Nakawari is hard-coded to never write `d3d12.dll`, `scskiller.*` or `steam_appid.txt` (`src/Core/SafeFs.cs`), writes only while a game is closed, and only copies a file when its contents differ, so SCSKiller's install watcher stays armed. The SHADERS column drives SCSKiller through its own command line: status, compile at idle priority, and turn on recording. SCSKiller itself is never bundled.

## Command line

```
Nakawari.exe --update-all                       update every enabled game (what sign-in runs)
Nakawari.exe --enable  "<game folder>" [0|2-6]  enable one game (0 = use the game's menu)
Nakawari.exe --disable "<game folder>"          remove it and restore the originals
Nakawari.exe --doctor  "<game folder>"          health report for one game
Nakawari.exe --kit     "<game folder>" on|off   put the 50-series kit in, or take it out
Nakawari.exe --pacing  "<game folder>" <preset> apply a frame-pacing preset
Nakawari.exe --report  [file]                   write what the scan sees
```

Results go to the exit code and to `%LOCALAPPDATA%\AbudiHadi\Nakawari\log.txt`. Nakawari stores nothing beside its exe and nothing inside a game folder except the files listed above.

## What to expect

- Your card gets the 3x/4x multipliers, not RTX 50 hardware. RTX 40 lacks Blackwell's hardware flip metering, so frame pacing and latency are worse than on a 50-series card. Start from a base frame rate of about 60 fps. The per-game **Pacing** presets help.
- Single-player games only. Nakawari refuses games with online anti-cheat.
- Vulkan games (Indiana Jones, DOOM: The Dark Ages) need the addon's experimental Vulkan path. Nakawari flags them.

## What is mine and what is borrowed

Nakawari does not contain any frame-generation code of its own. It is the safe installer, updater and health-check around other people's work.

**Written by AbudiHadi for this project (MIT):**

- The whole WPF app: library scanners for Steam, Epic, GOG and manual folders, the DLSS-G probe, the anti-cheat guard, the ledger and backup vault with byte-exact restore, the ReShade and addon installers, the local DLSS-G harvester with Authenticode checks, the doctor (health reader, ceiling book, crash watch, self-repair), the 50-series kit, pacing presets, the SCSKiller bridge and CLI driver, the sign-in updater, the command line, Day and Night themes, and the Velopack packaging.
- `profiles.json`, the per-game fixes list.
- The original watercolor art and the mascot, which stay with the author and are **not** in this repository.

**Open source and third-party components, and how each is used:**

| Component | Author | License | Use |
|---|---|---|---|
| [MFG Unlock addon](https://github.com/mavismmg/MFGAdaUnlock-RenoDx) | ImDreamt (original author and copyright holder), maintained fork by mavismmg, with dashdogy's research | MIT | The actual unlock. Downloaded at run time from its GitHub releases; not in this repo. |
| [ReShade](https://reshade.me) | crosire | BSD-3-Clause | The addon loader (`dxgi.dll`). Downloaded at run time from reshade.me; not in this repo. |
| [SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) | BlueHeisenberg | GPL-3.0 | Shader pre-compilation. Called as a separate program through its CLI; nothing bundled or copied. |
| [Velopack](https://github.com/velopack/velopack) | Velopack contributors | MIT | Installer and self-update. Bundled as a NuGet package. |
| [Varela Round](https://fonts.google.com/specimen/Varela+Round) | The Varela Round Project Authors | SIL OFL 1.1 | UI font. Bundled in `src/Assets/Fonts`. |
| [.NET 10 and WPF](https://github.com/dotnet/wpf) | Microsoft | MIT | Runtime and UI framework. Bundled in the self-contained build. |
| [NVIDIA Streamline](https://github.com/NVIDIA-RTX/Streamline) runtime, DLSS-G | NVIDIA | NVIDIA's terms | Only for the opt-in 50-series kit, fetched from NVIDIA's official release and signature-checked. DLSS-G DLLs otherwise come only from games already on your PC. Nothing from NVIDIA is in this repo or the installer. |

Full notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Nakawari is not affiliated with or endorsed by NVIDIA, ReShade, RenoDX or SCSKiller.

## Build

Requirements: Windows 10/11, the .NET 10 SDK, and for the installer the Velopack CLI (`dotnet tool install -g vpk`).

```powershell
dotnet build src\Nakawari.csproj -c Release     # must finish with 0 warnings
powershell -File publish.ps1                     # single self-contained publish\Nakawari.exe
powershell -File release.ps1 2.0.0 -LocalOnly    # Velopack Setup.exe in build\releases
```

Test with a sandbox copy of a game folder: `Nakawari.exe --enable "<sandbox folder>"` then `--disable`. Never test on a real game folder.

### Art

`src/Assets` holds placeholder graphics drawn by `tools/make-placeholder-assets.ps1`. To use your own paintings, put `day.png`, `night.png`, `mascot.png` and `icon.png` in `art/raw` and run `tools/make-assets.ps1`.

### Code layout

One job per class. `Core/` is plumbing, `Services/` is behaviour, `Models/` is data, `ViewModels/` is UI state.

- `src/Core/`: safe file I/O (`SafeFs`), INI editing, HTTP, PE and Authenticode readers, JSON store
- `src/Services/Library/`: one scanner per store
- `src/Services/`: probe, anti-cheat guard, ReShade, addon, DLSS-G, installer, maintainer, backup vault, ledgers, SCSKiller bridge
- `src/Services/Doctor/`: health reader, ceiling book, crash watch
- `src/Services/Kit/`: the 50-series kit
- `src/ViewModels/` and `src/*.xaml`: the UI
- `docs/`: architecture decisions and known issues

The rules every change follows are in [CLAUDE.md](CLAUDE.md).

## License

MIT. Copyright (c) 2026 AbudiHadi. See [LICENSE](LICENSE).
