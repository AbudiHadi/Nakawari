using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Keeps a copy of any game-folder file Nakawari is about to overwrite, and records it in the ledger,
/// so Disable puts back exactly what was there before (a hand-installed addon, the game's own DLSS-G, ...).
/// </summary>
public static class BackupVault
{
    public static void Preserve(string file, string role, Ledger ledger)
    {
        if (!File.Exists(file)) return;

        var folder = Directory.CreateDirectory(Path.Combine(AppPaths.Backups, ledger.GameId)).FullName;
        var backup = Path.Combine(folder, Pe.Sha256Text(file)[..12] + "_" + Path.GetFileName(file));
        File.Copy(file, backup, overwrite: true);

        var entry = ledger.Files.FirstOrDefault(f => f.Path.Equals(file, StringComparison.OrdinalIgnoreCase));
        if (entry is null) ledger.Track(file, role, backup);
        else entry.BackupPath = backup;
    }

    /// <summary>True when the ledger already owns this path, so its current content is Nakawari's, not the user's.</summary>
    public static bool IsTracked(string file, Ledger ledger) =>
        ledger.Files.Any(f => f.Path.Equals(file, StringComparison.OrdinalIgnoreCase));
}
