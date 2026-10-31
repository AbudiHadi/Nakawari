# Nakawari: project rules

Nakawari (中割り, "in-between frames") is a Windows WPF app by AbudiHadi. It turns on DLSS Multi Frame Generation for RTX 40 by installing ReShade (`dxgi.dll`) and the MFG Unlock addon into game folders. It also works with SCSKiller's shader cache.

## Principles (owner's standing rules)
- **OOP with service classes.** One job per class. `Services/` holds the behaviour, `Core/` holds the plumbing, `Models/` holds data, and `ViewModels/` holds the UI state.
- **KISS.** Small, single-purpose methods.
- **DRY.** Reuse `SafeFs`, `BackupVault`, `JsonStore`, `Http` and `AppInfo`. Never re-implement them.
- **Small files.** Split any file that grows past one job.
- **Out of the box.** The published app needs no installs from the user.

## Hard rules
- **Every write into a game folder goes through `Core/SafeFs`.** It refuses SCSKiller's files (`d3d12.dll`, `scskiller.*`, `steam_appid.txt`).
- **Before overwriting a file the ledger doesn't own, call `BackupVault.Preserve`.** Disable must restore the folder byte for byte.
- **Never write into a game folder while that game is running** (`SystemInfo.IsRunning`).
- **Drive SCSKiller only through its CLI's non-interactive commands, with stdin closed.** Never copy or bundle its GPL code.
- **Get DLSS-G DLLs only from games already on this PC** (`DlssgService.Harvest`). The one exception is the opt-in 50-series kit (ADR-005): it fetches NVIDIA's official Streamline release and keeps only NVIDIA-signed files. Never download NVIDIA binaries from anywhere else.
- **Store nothing beside the exe.** The installer replaces that folder on every update. All data goes in `%LOCALAPPDATA%\AbudiHadi\Nakawari`.

## Layout
- `src/`: the app (.NET 10, WPF)
- `src/Assets/`: fonts and placeholder graphics. The original paintings are not in this repository.
- `tools/make-placeholder-assets.ps1`: draws the placeholder graphics into `src/Assets`
- `tools/make-assets.ps1`: turns your own paintings in `art/raw` into `src/Assets` (optional)
- `docs/`: decisions and known issues
- `publish.ps1`: release build

## Verify a change
1. `dotnet build -c Release` must finish with 0 warnings.
2. Test with `Nakawari.exe --enable/--disable "<sandbox folder>"`. Never use a real game folder for tests.
3. For UI changes, take screenshots in both Day and Night themes at 150% DPI.
