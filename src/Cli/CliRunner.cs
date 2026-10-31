using System.Text;
using Nakawari.Core;
using Nakawari.Models;
using Nakawari.Services;

namespace Nakawari.Cli;

/// <summary>
/// Windowless modes (results go to the log and the exit code):
///   --update-all                       update every enabled game (used at Windows sign-in)
///   --enable  &lt;game folder&gt; [2..6|0]   enable MFG for one game
///   --disable &lt;game folder&gt;            remove it again, restoring the original files
///   --kit     &lt;game folder&gt; on|off      add or remove the 50-series kit for an enabled game
///   --pacing  &lt;game folder&gt; &lt;preset&gt;    GameDefault | SmoothVrr | LowLatency | FreezeFix
///   --doctor  &lt;folder with ReShade.log&gt;  read the last session's health
///   --report  [file]                   write what a scan sees, for troubleshooting
/// </summary>
public static class CliRunner
{
    private static readonly string[] Verbs = ["--update-all", "--enable", "--disable", "--kit", "--pacing", "--report", "--doctor"];

    public static bool Handles(string[] args) => args.Length > 0 && Verbs.Contains(args[0]);

    /// <summary>Windowless runs never show a dialog: any unexpected error goes to the log and the exit code.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        try { return await RunCoreAsync(args); }
        catch (Exception ex)
        {
            Log.Error($"CLI {args[0]} failed", ex);
            return 2;
        }
    }

    private static async Task<int> RunCoreAsync(string[] args)
    {
        var services = new AppServices();
        var result = args[0] switch
        {
            "--update-all" => await UpdateAllAsync(services),
            "--enable" when args.Length > 1 => await EnableAsync(services, args[1], args.Length > 2 ? int.Parse(args[2]) : 0),
            "--disable" when args.Length > 1 => Disable(services, args[1]),
            "--kit" when args.Length > 2 => await KitAsync(services, args[1], args[2] == "on"),
            "--pacing" when args.Length > 2 && Enum.TryParse<PacingPreset>(args[2], true, out var preset) =>
                LedgerFor(services, args[1]) is { } paced ? services.Installer.SetPacing(paced, preset) : NotEnabled(args[1]),
            "--doctor" when args.Length > 1 => Doctor(args[1]),
            "--report" => Report(services, args.Length > 1 ? args[1] : Path.Combine(AppPaths.Root, "report.txt")),
            _ => OpResult.Fail($"Missing game folder for {args[0]}"),
        };
        Log.Info($"CLI {args[0]}: {result.Message}");
        return result.Ok ? 0 : 1;
    }

    private static async Task<OpResult> UpdateAllAsync(AppServices services)
    {
        var results = await services.SignInRoutineAsync();
        return new OpResult(results.All(r => r.Ok), $"{results.Count} game(s) checked");
    }

    private static async Task<OpResult> EnableAsync(AppServices services, string folder, int multiplier)
    {
        var dir = Path.GetFullPath(folder).TrimEnd('\\');
        var match = services.ScanAll().FirstOrDefault(r => r.Game.InstallDir.TrimEnd('\\').Equals(dir, StringComparison.OrdinalIgnoreCase));
        var game = match.Game ?? Services.Library.ManualLibrary.EntryFor(dir);
        var probe = match.Probe ?? services.Probe.Probe(game);
        return await services.Installer.EnableAsync(game, probe, multiplier);
    }

    private static OpResult Disable(AppServices services, string folder) =>
        LedgerFor(services, folder) is { } ledger ? services.Installer.Disable(ledger) : NotEnabled(folder);

    private static async Task<OpResult> KitAsync(AppServices services, string folder, bool on)
    {
        if (LedgerFor(services, folder) is not { } ledger) return NotEnabled(folder);
        return on ? await services.KitInstaller.InstallAsync(ledger) : services.KitInstaller.Remove(ledger);
    }

    private static Ledger? LedgerFor(AppServices services, string folder)
    {
        var dir = Path.GetFullPath(folder).TrimEnd('\\');
        return services.Ledgers.All().FirstOrDefault(l => l.ExePath.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static OpResult NotEnabled(string folder) => OpResult.Fail($"MFG is not enabled for {folder}");

    private static OpResult Doctor(string folder)
    {
        var health = Services.Doctor.HealthReader.Read(Path.GetFullPath(folder), DateTime.MinValue);
        return new OpResult(health.State is not Models.HealthState.Problem and not Models.HealthState.ReShadeNotLoading,
            $"{health.State}: {health.Detail} (max {health.MaxMultiplier?.ToString() ?? "?"}x)");
    }

    private static OpResult Report(AppServices services, string path)
    {
        var text = new StringBuilder();
        foreach (var (game, probe) in services.ScanAll())
        {
            text.AppendLine($"{game.Store,-7} {game.Name}");
            text.AppendLine($"        dir      {game.InstallDir}");
            text.AppendLine($"        exe      {probe.ExePath ?? "-"}");
            text.AppendLine($"        dlssg    {probe.DlssgVersion?.ToString() ?? "-"} ({probe.DlssgFiles.Count} file(s))");
            text.AppendLine($"        streamline {probe.StreamlineVersion?.ToString() ?? "-"} (max {services.Ceilings.Predict(probe.StreamlineVersion)?.ToString() ?? "?"}x)");
            text.AppendLine($"        status   {services.Installer.Blocker(probe) ?? "ready"}");
            text.AppendLine($"        scskiller {ScsKillerBridge.StateOf(probe.ExeDir)}");
            if (probe.AntiCheat is not null) text.AppendLine($"        anticheat {probe.AntiCheat}");
        }
        File.WriteAllText(path, text.ToString());
        return OpResult.Success($"report written to {path}");
    }
}
