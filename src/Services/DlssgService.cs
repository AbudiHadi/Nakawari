using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// The addon needs a modern nvngx_dlssg.dll. Instead of scraping a download site, the newest
/// copy already shipped by any installed game is kept as the reference. A newer DLL dropped
/// into the cache folder by hand wins the same way.
/// </summary>
public sealed class DlssgService
{
    public const string FileName = "nvngx_dlssg.dll";

    public static string CacheFolder => AppPaths.DlssgCache;
    private static string Cached => Path.Combine(AppPaths.DlssgCache, FileName);

    public Version? CachedVersion => File.Exists(Cached) ? Pe.FileVersion(Cached) : null;

    /// <summary>
    /// Keeps the newest NVIDIA-signed DLSS-G found in installed games. Unsigned or tampered copies
    /// (mod folders, repacks) are never used, however high a version they claim.
    /// </summary>
    public void Harvest(IEnumerable<string> dlssgFiles)
    {
        if (File.Exists(Cached) && !Authenticode.IsSignedBy(Cached, Publisher))
        {
            File.Delete(Cached);   // picked before signatures were checked; never trust it again
            Log.Info("Dropped an unsigned reference DLSS-G");
        }

        var current = CachedVersion;
        var best = dlssgFiles
            .Select(f => (Path: f, Version: Pe.FileVersion(f)))
            .Where(f => f.Version is not null && (current is null || f.Version > current))
            .OrderByDescending(f => f.Version)
            .FirstOrDefault(f => Authenticode.IsSignedBy(f.Path, Publisher));
        if (best.Path is null) return;

        File.Copy(best.Path, Cached, overwrite: true);
        Log.Info($"Reference DLSS-G is now {best.Version} (NVIDIA-signed) from {best.Path}");
    }

    private const string Publisher = "NVIDIA Corporation";

    public bool IsNewerThan(Version? gameVersion) =>
        gameVersion is not null && CachedVersion is { } cached && cached > gameVersion;

    /// <summary>
    /// Backs up the game's own copy, then replaces it with the reference DLL. The backup is
    /// refreshed when a game patch shipped a different DLL since the last upgrade.
    /// </summary>
    public void Upgrade(IEnumerable<string> gameFiles, Ledger ledger)
    {
        foreach (var file in gameFiles)
        {
            if (!Pe.SameContent(file, Cached)) BackupVault.Preserve(file, LedgerFile.Dlssg, ledger);
            SafeFs.Copy(Cached, file);
        }
    }
}
