using Nakawari.Core;
using Velopack;
using Velopack.Sources;

namespace Nakawari.Services;

/// <summary>
/// Nakawari updating itself from its GitHub releases (Velopack). Only the window uses this, never the
/// windowless sign-in run, so an update can't restart the app in the middle of a game-folder change.
/// A copy that wasn't installed through Setup (e.g. a portable exe) simply never offers updates.
/// </summary>
public sealed class AppUpdateService
{
    private readonly UpdateManager _manager = new(new GithubSource(AppInfo.RepoUrl, accessToken: null, prerelease: false));
    private UpdateInfo? _pending;

    public bool IsInstalled => _manager.IsInstalled;
    public string? PendingVersion => _pending?.TargetFullRelease.Version.ToString();

    public async Task<string?> CheckAsync()
    {
        if (!IsInstalled) return null;
        try
        {
            _pending = await _manager.CheckForUpdatesAsync();
            return PendingVersion;
        }
        catch (Exception ex)
        {
            // Offline, rate-limited, or the repo is private for now: simply no update offer.
            Log.Error("App update check skipped", ex);
            return null;
        }
    }

    /// <summary>Downloads the new version, then restarts into it.</summary>
    public async Task ApplyAndRestartAsync()
    {
        if (_pending is null) return;
        await _manager.DownloadUpdatesAsync(_pending);
        Log.Info($"Restarting into Nakawari {PendingVersion}");
        _manager.ApplyUpdatesAndRestart(_pending);
    }
}
