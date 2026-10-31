using System.Diagnostics.Eventing.Reader;
using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services.Doctor;

/// <summary>
/// Counts Windows "Application Error" (event 1000) crashes that are plausibly caused by what Nakawari
/// installed: same game EXE, after Nakawari's last change, faulting in a frame-generation module
/// (the addon, ReShade's dxgi.dll in the game folder, DLSS-G or Streamline). A crash in the game's
/// own code is not counted.
/// </summary>
public static class CrashWatch
{
    private const int AppPathIndex = 10;
    private const int ModulePathIndex = 11;

    public static int CountSince(Ledger ledger)
    {
        var since = ledger.LastChangedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var query = new EventLogQuery("Application", PathType.LogName,
            $"*[System[Provider[@Name='Application Error'] and (EventID=1000) and TimeCreated[@SystemTime>='{since}']]]");

        var count = 0;
        try
        {
            using var reader = new EventLogReader(query);
            for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
            {
                using (record)
                {
                    if (IsOurs(record, ledger)) count++;
                }
            }
        }
        catch (EventLogException ex)
        {
            Log.Error("Could not read the Application event log", ex);
        }
        return count;
    }

    private static bool IsOurs(EventRecord record, Ledger ledger)
    {
        if (record.Properties.Count <= ModulePathIndex) return false;
        var appPath = record.Properties[AppPathIndex].Value as string;
        var modulePath = record.Properties[ModulePathIndex].Value as string;
        if (appPath is null || modulePath is null) return false;
        if (!appPath.Equals(ledger.ExePath, StringComparison.OrdinalIgnoreCase)) return false;

        var module = Path.GetFileName(modulePath);
        return ledger.Files.Any(f => f.Path.Equals(modulePath, StringComparison.OrdinalIgnoreCase))
            || module.Equals(DlssgService.FileName, StringComparison.OrdinalIgnoreCase)
            || module.StartsWith("sl.", StringComparison.OrdinalIgnoreCase);
    }
}
