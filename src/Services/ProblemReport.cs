using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Nakawari.Core;

namespace Nakawari.Services;

/// <summary>
/// "Report a problem": packs everything needed to diagnose an issue into one zip on the Desktop,
/// then opens a pre-filled GitHub issue. Nothing is uploaded by Nakawari; the user attaches the zip.
/// </summary>
public static class ProblemReport
{
    private const int LogTailLines = 400;

    public static string Create(AppServices services, string? gpu)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, $"Nakawari-report-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            AddText(zip, "system.txt", SystemSummary(services, gpu));
            AddFile(zip, AppPaths.LogFile, "nakawari-log.txt");
            AddFile(zip, AppPaths.SettingsFile, "settings.json");
            AddFile(zip, Path.Combine(AppPaths.Root, "ceilings.json"), "ceilings.json");

            foreach (var ledger in services.Ledgers.All())
            {
                var folder = Safe(ledger.GameName);
                AddText(zip, $"games/{folder}/ledger.json", System.Text.Json.JsonSerializer.Serialize(ledger, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                AddTail(zip, Path.Combine(ledger.ExeDir, "ReShade.log"), $"games/{folder}/ReShade.log");
                AddFile(zip, Path.Combine(ledger.ExeDir, "ReShade.ini"), $"games/{folder}/ReShade.ini");
            }
        }

        Log.Info($"Problem report written to {path}");
        return path;
    }

    /// <summary>Shows the zip in Explorer and opens a new GitHub issue with the basics filled in.</summary>
    public static void Open(string zipPath, string? gpu)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });

        var body = $"**What happened?**\n\n\n**What did you expect?**\n\n\n---\nNakawari {AppInfo.Version} · {gpu ?? "GPU unknown"} · {Environment.OSVersion.VersionString}\n" +
                   $"Please drag `{Path.GetFileName(zipPath)}` from your Desktop into this box.";
        var url = $"{AppInfo.RepoUrl}/issues/new?title={Uri.EscapeDataString("Problem: ")}&body={Uri.EscapeDataString(body)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string SystemSummary(AppServices services, string? gpu)
    {
        var text = new StringBuilder();
        text.AppendLine($"Nakawari      {AppInfo.Version}");
        text.AppendLine($"Windows       {Environment.OSVersion.VersionString}");
        text.AppendLine($"GPU / driver  {gpu ?? "unknown"}");
        text.AppendLine($"Addon cached  {string.Join(", ", services.Addon.CachedTags())}");
        text.AppendLine($"DLSS-G ref    {services.Dlssg.CachedVersion?.ToString() ?? "-"}");
        text.AppendLine($"SCSKiller     {(services.ShaderCache.IsInstalled ? services.ShaderCache.Version : "not installed")}");
        text.AppendLine($"Enabled games {services.Ledgers.All().Count}");
        return text.ToString();
    }

    private static void AddFile(ZipArchive zip, string path, string name)
    {
        if (!File.Exists(path)) return;
        try { zip.CreateEntryFromFile(path, name); }
        catch (IOException) { AddText(zip, name + ".unreadable.txt", "File was in use when the report was made."); }
    }

    private static void AddTail(ZipArchive zip, string path, string name)
    {
        if (!File.Exists(path)) return;
        try { AddText(zip, name, string.Join(Environment.NewLine, File.ReadLines(path).TakeLast(LogTailLines))); }
        catch (IOException) { AddText(zip, name + ".unreadable.txt", "Log was in use (game running?)."); }
    }

    private static void AddText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string Safe(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
