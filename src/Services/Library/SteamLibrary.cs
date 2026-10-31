using Microsoft.Win32;
using Nakawari.Models;

namespace Nakawari.Services.Library;

public sealed class SteamLibrary : ILibrarySource
{
    // Tools and runtimes that are never games.
    private static readonly HashSet<string> SkipAppIds = ["228980", "1070560", "1391110", "1628350", "1493710"];

    public string Store => "Steam";

    public IReadOnlyList<GameEntry> Discover()
    {
        var root = Root();
        if (root is null || !Directory.Exists(root)) return [];

        var games = new List<GameEntry>();
        foreach (var library in LibraryFolders(root))
        {
            var installed = Manifests(library).ToList();
            games.AddRange(installed);
            games.AddRange(LooseFolders(library, installed));
        }
        return games;
    }

    /// <summary>Steam's install folder, from the registry.</summary>
    public static string? Root() =>
        (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string)?.Replace('/', '\\');

    /// <summary>Game folders under steamapps\common with no manifest (copied in, or left after a reinstall).</summary>
    private IEnumerable<GameEntry> LooseFolders(string library, IReadOnlyList<GameEntry> installed)
    {
        var common = Path.Combine(library, "steamapps", "common");
        if (!Directory.Exists(common)) return [];

        var known = installed.Select(g => g.InstallDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateDirectories(common)
            .Where(dir => !known.Contains(dir))
            .Select(dir => new GameEntry($"steamdir_{Core.Pe.Sha256Text(dir)[..12]}", Path.GetFileName(dir), "Steam folder", dir));
    }

    private static IEnumerable<string> LibraryFolders(string root)
    {
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        var paths = File.Exists(vdf)
            ? Vdf.Pairs(vdf).Where(p => p.Key == "path").Select(p => p.Value)
            : [];
        return paths.Append(root).Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);
    }

    private IEnumerable<GameEntry> Manifests(string library)
    {
        var steamapps = Path.Combine(library, "steamapps");
        if (!Directory.Exists(steamapps)) yield break;

        foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
        {
            var pairs = Vdf.Pairs(manifest).ToList();
            var appId = Vdf.First(pairs, "appid");
            var name = Vdf.First(pairs, "name");
            var installDir = Vdf.First(pairs, "installdir");
            if (appId is null || name is null || installDir is null || SkipAppIds.Contains(appId)) continue;

            var dir = Path.Combine(steamapps, "common", installDir);
            if (Directory.Exists(dir)) yield return new GameEntry($"steam_{appId}", name, Store, dir);
        }
    }
}
