using System.Diagnostics;
using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services.ShaderCache;

/// <summary>One game's shader-cache state as SCSKiller reports it.</summary>
public sealed record ShaderStatus(string Label, string Detail);

/// <summary>
/// Drives SCSKiller (GPL-3.0, by BlueHeisenberg) through its own command line as a separate process.
/// Nothing of SCSKiller is bundled or copied. Only commands that never prompt are used, stdin is closed,
/// and games are addressed by exact Steam id (steam:&lt;appid&gt;), never by a name guess.
/// </summary>
public sealed class ScsKillerCli
{
    /// <summary>The SCSKiller version whose output Nakawari was written against.</summary>
    public const string TestedVersion = "1.2";

    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCSKiller.App");
    private static string CliPath => Path.Combine(AppDir, "current", "cli", "scskiller.exe");

    public bool IsInstalled => File.Exists(CliPath);

    public string? Version => File.Exists(Path.Combine(AppDir, "current", "SCSKiller.Core.dll"))
        ? Pe.FileVersion(Path.Combine(AppDir, "current", "SCSKiller.Core.dll"))?.ToString(3)
        : null;

    /// <summary>True when this SCSKiller's output format is one Nakawari knows how to read.</summary>
    public bool IsKnownVersion => Version?.StartsWith(TestedVersion + ".", StringComparison.Ordinal) == true;

    public static string? IdFor(GameEntry game) =>
        game.Id.StartsWith("steam_", StringComparison.Ordinal) ? "steam:" + game.Id["steam_".Length..] : null;

    public async Task<ShaderStatus?> StatusAsync(string id)
    {
        if (!IsInstalled || !IsKnownVersion) return null;
        var (exit, output) = await RunAsync($"status {id}", TimeSpan.FromSeconds(20), CancellationToken.None);
        if (exit != 0) return null;

        var status = Field(output, "status");
        if (status is null) return null;
        var head = status.Split(':', 2)[0].Trim();
        return new ShaderStatus(Humanize(head), status);
    }

    public async Task<OpResult> CompileAsync(string id, string gameName, CancellationToken cancel)
    {
        var (exit, output) = await RunAsync($"compile {id} --idle", TimeSpan.FromHours(2), cancel);
        return exit == 0
            ? OpResult.Success($"{gameName}: shaders compiled ahead of time by SCSKiller")
            : OpResult.Fail($"{gameName}: SCSKiller compile stopped ({LastLine(output)})");
    }

    public async Task<OpResult> RecordAsync(string id, string gameName)
    {
        var (exit, output) = await RunAsync($"record install {id}", TimeSpan.FromMinutes(2), CancellationToken.None);
        return exit == 0
            ? OpResult.Success($"{gameName}: SCSKiller recorder on. Play about 5 minutes, then compile")
            : OpResult.Fail($"{gameName}: SCSKiller couldn't turn on recording ({LastLine(output)})");
    }

    public void OpenApp()
    {
        var app = Path.Combine(AppDir, "SCSKiller.exe");
        if (File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
    }

    private static async Task<(int Exit, string Output)> RunAsync(string arguments, TimeSpan timeout, CancellationToken cancel)
    {
        var start = new ProcessStartInfo(CliPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("SCSKiller did not start.");
        process.StandardInput.Close();   // any unexpected question gets "no answer" instead of hanging

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(limit.Token);
        var errors = process.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, "cancelled");
        }
        return (process.ExitCode, await output + await errors);
    }

    private static string? Field(string output, string key) =>
        output.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith(key + " ", StringComparison.Ordinal))?[key.Length..].Trim();

    /// <summary>"NeedsRecording" -> "Needs recording".</summary>
    private static string Humanize(string pascal)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(pascal, "(?<!^)([A-Z])", " $1").ToLowerInvariant();
        return words.Length == 0 ? pascal : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static string LastLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "no output";
}
