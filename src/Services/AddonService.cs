using System.Text.Json;
using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

public sealed record AddonRelease(string Tag, string Url, long Size);

/// <summary>Fetches and caches the MFG Unlock addon from its GitHub releases.</summary>
public sealed class AddonService(AppSettings settings)
{
    public const string FileName = "renodx-mfgunlock.addon64";
    private static string CurrentTagFile => Path.Combine(AppPaths.AddonCache, "current.txt");

    private AddonRelease? _latest;

    public string? CachedTag => File.Exists(CurrentTagFile) ? File.ReadAllText(CurrentTagFile).Trim() : null;

    public async Task<AddonRelease> LatestAsync()
    {
        if (_latest is not null) return _latest;

        var json = await Http.Client.GetStringAsync($"https://api.github.com/repos/{settings.AddonRepo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var asset = root.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString() == FileName);
        if (asset.ValueKind == JsonValueKind.Undefined)
            throw new IOException($"Latest release of {settings.AddonRepo} has no {FileName}.");

        _latest = new AddonRelease(
            root.GetProperty("tag_name").GetString()!,
            asset.GetProperty("browser_download_url").GetString()!,
            asset.GetProperty("size").GetInt64());
        return _latest;
    }

    /// <summary>Every addon build already downloaded, oldest first.</summary>
    public IReadOnlyList<string> CachedTags() =>
        Directory.EnumerateDirectories(AppPaths.AddonCache)
            .Select(System.IO.Path.GetFileName)
            .OfType<string>()
            .Where(tag => File.Exists(PathFor(tag)))
            .OrderBy(TagOrder)
            .ToList();

    /// <summary>Sorts "1.4.3" style tags numerically; anything else sorts first.</summary>
    public static Version TagOrder(string tag) => Version.TryParse(tag.TrimStart('v'), out var v) ? v : new Version(0, 0);

    /// <summary>
    /// A specific cached build (used after a rollback), or else the latest release.
    /// Falls back to the last cached build when GitHub is unreachable.
    /// </summary>
    public async Task<(string Tag, string Path)> EnsureCachedAsync(string? tag = null)
    {
        if (tag is not null && File.Exists(PathFor(tag))) return (tag, PathFor(tag));
        try
        {
            var release = await LatestAsync();
            var path = PathFor(release.Tag);
            if (!File.Exists(path))
            {
                await Http.DownloadAsync(release.Url, path, release.Size);
                Log.Info($"Downloaded MFG Unlock {release.Tag}");
            }
            File.WriteAllText(CurrentTagFile, release.Tag);
            return (release.Tag, path);
        }
        catch (HttpRequestException ex) when (CachedTag is { } cachedTag && File.Exists(PathFor(cachedTag)))
        {
            Log.Error("GitHub unreachable, using cached addon", ex);
            return (cachedTag, PathFor(cachedTag));
        }
    }

    public static string PathFor(string tag) => Path.Combine(AppPaths.AddonCache, tag, FileName);
}
