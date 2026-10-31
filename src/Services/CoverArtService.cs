using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Game header art for the list. Steam games only: Steam's own local library cache first,
/// then Steam's public CDN once, cached under the app's folder. Other stores get the mascot tile.
/// </summary>
public sealed class CoverArtService
{
    private const int PlaceholderBytes = 8 * 1024;   // Steam leaves ~2 KB stubs for art it hasn't fetched

    private static readonly string? SteamCache = Library.SteamLibrary.Root() is { } root
        ? Path.Combine(root, "appcache", "librarycache")
        : null;

    private static string CoverCache => Directory.CreateDirectory(Path.Combine(AppPaths.Root, "cache", "covers")).FullName;

    public async Task<string?> FindAsync(GameEntry game)
    {
        if (!game.Id.StartsWith("steam_", StringComparison.Ordinal)) return null;
        var appId = game.Id["steam_".Length..];

        var local = FromSteamCache(appId);
        if (local is not null) return local;

        var cached = Path.Combine(CoverCache, appId + ".jpg");
        if (File.Exists(cached)) return cached;

        try
        {
            await Http.DownloadAsync($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg", cached);
            return cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return null;
        }
    }

    private static string? FromSteamCache(string appId)
    {
        var folder = SteamCache is null ? null : Path.Combine(SteamCache, appId);
        if (folder is null || !Directory.Exists(folder)) return null;

        return Directory.EnumerateFiles(folder, "library_header.jpg", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(folder, "header.jpg", SearchOption.AllDirectories))
            .FirstOrDefault(f => new FileInfo(f).Length > PlaceholderBytes);
    }
}
