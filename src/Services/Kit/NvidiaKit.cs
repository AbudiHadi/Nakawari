using System.IO.Compression;
using Nakawari.Core;

namespace Nakawari.Services.Kit;

/// <summary>
/// The "50-series kit": NVIDIA's own Streamline 2.14.1 runtime with DLSS-G 310.9.1, the exact stack
/// the MFG Unlock addon validates for Dynamic MFG and the full 6x ceiling. Pulled only from NVIDIA's
/// official GitHub release, only the production x64 runtime DLLs, and every file must carry a valid
/// NVIDIA signature before it is kept. A zip the user downloaded themselves can be imported instead.
/// </summary>
public sealed class NvidiaKit
{
    public const string Version = "2.14.1";
    public const string OfficialUrl = "https://github.com/NVIDIA-RTX/Streamline/releases/download/v2.14.1/streamline-sdk-v2.14.1.zip";
    private const string RuntimeFolder = "bin/x64/";
    private const string Publisher = "NVIDIA Corporation";

    public static string Folder => Path.Combine(AppPaths.Root, "cache", "kit", Version);
    private static string ReadyMarker => Path.Combine(Folder, ".verified");

    public bool IsReady => File.Exists(ReadyMarker);

    public IReadOnlyList<string> FileNames =>
        IsReady ? Directory.EnumerateFiles(Folder, "*.dll").Select(f => Path.GetFileName(f)!).ToList() : [];

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    /// <summary>True when this file is byte-identical to the kit's copy (so it isn't harvested as a "game" DLL).</summary>
    public bool IsKitCopy(string path) => IsReady && Pe.SameContent(path, PathOf(Path.GetFileName(path)));

    /// <summary>Downloads just the runtime DLLs out of NVIDIA's official release with range requests.</summary>
    public async Task EnsureAsync()
    {
        if (IsReady) return;
        var zip = new RemoteZip(OfficialUrl);
        var entries = Runtime(await zip.ListAsync(), e => e.Name);
        await KeepVerifiedAsync(entries.Select(e => (Name: Path.GetFileName(e.Name), Extract: (Func<string, Task>)(target => zip.ExtractAsync(e, target)))));
    }

    /// <summary>Imports from a streamline-sdk-v2.14.1.zip the user downloaded from NVIDIA themselves.</summary>
    public async Task ImportAsync(string zipPath)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var entries = Runtime(archive.Entries, e => e.FullName);
        await KeepVerifiedAsync(entries.Select(e => (Name: e.Name, Extract: (Func<string, Task>)(target =>
        {
            e.ExtractToFile(target, overwrite: true);
            return Task.CompletedTask;
        }))));
    }

    private static List<T> Runtime<T>(IEnumerable<T> entries, Func<T, string> name)
    {
        var runtime = entries
            .Where(e => name(e).StartsWith(RuntimeFolder, StringComparison.OrdinalIgnoreCase)
                        && name(e).LastIndexOf('/') == RuntimeFolder.Length - 1       // production only, not development/
                        && IsRuntimeDll(Path.GetFileName(name(e))))
            .ToList();
        if (runtime.Count == 0) throw new InvalidDataException("No Streamline runtime DLLs found where expected (bin/x64).");
        return runtime;
    }

    private static async Task KeepVerifiedAsync(IEnumerable<(string Name, Func<string, Task> Extract)> files)
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        Directory.CreateDirectory(Folder);
        var count = 0;
        foreach (var (name, extract) in files)
        {
            var target = Path.Combine(Folder, name);
            await extract(target);
            if (!Authenticode.IsSignedBy(target, Publisher))
            {
                Directory.Delete(Folder, recursive: true);
                throw new InvalidDataException($"{name} is not signed by {Publisher}; the kit was discarded.");
            }
            count++;
        }
        File.WriteAllText(ReadyMarker, DateTime.Now.ToString("O"));
        Log.Info($"50-series kit ready: Streamline {Version} ({count} NVIDIA-signed files)");
    }

    private static bool IsRuntimeDll(string name) =>
        name.StartsWith("sl.", StringComparison.OrdinalIgnoreCase)
        || name.Equals(DlssgService.FileName, StringComparison.OrdinalIgnoreCase);
}
