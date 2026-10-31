using System.Text.RegularExpressions;
using Nakawari.Core;

namespace Nakawari.Services;

/// <summary>
/// ReShade (add-on build) is the loader the MFG Unlock addon needs. It goes in as dxgi.dll,
/// which does not collide with SCSKiller's d3d12.dll proxy; SCSKiller itself identifies ReShade
/// by the DLL's ProductName, which is the same check used here.
/// </summary>
public sealed partial class ReShadeService
{
    public const string ProxyName = "dxgi.dll";
    private static readonly string[] ProxyNames = ["dxgi.dll", "d3d11.dll", "dinput8.dll", "version.dll", "winmm.dll"];

    private string CachedDll => Path.Combine(AppPaths.ReShadeCache, "ReShade64.dll");
    private string CachedVersionFile => Path.Combine(AppPaths.ReShadeCache, "version.txt");

    [GeneratedRegex(@"downloads/ReShade_Setup_([\d.]+)_Addon\.exe")]
    private static partial Regex AddonInstallerLink();

    public static bool IsReShade(string path) =>
        File.Exists(path) && string.Equals(Pe.ProductName(path), "ReShade", StringComparison.OrdinalIgnoreCase);

    /// <summary>A ReShade the user (or another tool) already put next to the game, under any proxy name.</summary>
    public string? FindInstalled(string exeDir) =>
        ProxyNames.Select(n => Path.Combine(exeDir, n)).FirstOrDefault(IsReShade);

    /// <summary>Name of a non-ReShade dxgi.dll that would be overwritten, or null when the slot is free.</summary>
    public string? Conflict(string exeDir)
    {
        var dxgi = Path.Combine(exeDir, ProxyName);
        return File.Exists(dxgi) && !IsReShade(dxgi) ? Pe.ProductName(dxgi) ?? "unknown dxgi.dll mod" : null;
    }

    /// <summary>Downloads the official add-on installer from reshade.me once and keeps ReShade64.dll.</summary>
    public async Task<string> EnsureCachedAsync()
    {
        if (File.Exists(CachedDll)) return CachedDll;

        var page = await Http.Client.GetStringAsync("https://reshade.me");
        var link = AddonInstallerLink().Match(page);
        if (!link.Success) throw new IOException("Could not find the ReShade add-on download on reshade.me.");

        var installer = Path.Combine(AppPaths.ReShadeCache, "ReShade_Setup_Addon.exe");
        await Http.DownloadAsync("https://reshade.me/" + link.Value, installer);
        EmbeddedZip.ExtractEntry(installer, "ReShade64.dll", CachedDll);
        File.Delete(installer);
        File.WriteAllText(CachedVersionFile, link.Groups[1].Value);

        Log.Info($"Cached ReShade {link.Groups[1].Value} (add-on build)");
        return CachedDll;
    }
}
