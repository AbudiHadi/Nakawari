using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>Works out where a game's real EXE lives and whether it ships DLSS Frame Generation.</summary>
public sealed class GameProbe(ScsKillerBridge scsKiller, AntiCheatGuard antiCheat, ProfileCatalog profiles)
{
    private static readonly string[] NotTheGame =
    [
        "crash", "unins", "setup", "redist", "vc_", "vcredist", "dxsetup", "dotnet", "prereq", "report",
        "helper", "cefprocess", "webhelper", "easyanticheat", "start_protected_game", "launcherpatcher",
        "ue4prereq", "ueprereq", "installer", "updater", "benchmark", "touchup", "cleanup", "quicksfv",
    ];

    public ProbeResult Probe(GameEntry game)
    {
        FolderIndex index;
        try { index = FolderIndex.Build(game.InstallDir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Cannot read {game.InstallDir}", ex);
            return new ProbeResult(null, [], null, null, null, profiles.For(game.Name));
        }

        return new ProbeResult(
            ResolveExe(game, index),
            index.DlssgFiles,
            NewestVersion(index.DlssgFiles),
            NewestVersion(index.StreamlineFiles),
            antiCheat.Detect(index),
            profiles.For(game.Name));
    }

    private string? ResolveExe(GameEntry game, FolderIndex index)
    {
        foreach (var hint in new[] { game.ExeHint, scsKiller.ExeHint(game.InstallDir) })
        {
            if (hint is not null && File.Exists(hint) && !IsLauncherStub(hint, index)) return hint;
        }

        var candidates = index.Executables.Where(IsCandidate).ToList();
        return candidates.FirstOrDefault(e => e.EndsWith("-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase))
            ?? candidates.Where(e => index.DlssgFiles.Concat(index.StreamlineFiles).Any(d => SameFolder(d, e))).MaxBy(Size)
            ?? candidates.MaxBy(Size);
    }

    /// <summary>Unreal games launch a tiny stub in the root; the real binary is the -Shipping one.</summary>
    private static bool IsLauncherStub(string exe, FolderIndex index) =>
        !exe.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase) &&
        index.Executables.Any(e => e.EndsWith("-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase));

    private static bool IsCandidate(string exe)
    {
        var name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
        var path = exe.ToLowerInvariant();
        return !NotTheGame.Any(name.Contains)
            && !path.Contains(@"\_commonredist\") && !path.Contains(@"\redist\")
            && !path.Contains(@"\engine\binaries\") && !path.Contains(@"\thirdparty\");
    }

    private static Version? NewestVersion(IEnumerable<string> files) =>
        files.Select(Pe.FileVersion).OfType<Version>().DefaultIfEmpty().Max();

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.GetDirectoryName(a), Path.GetDirectoryName(b), StringComparison.OrdinalIgnoreCase);

    private static long Size(string path) => new FileInfo(path).Length;
}
