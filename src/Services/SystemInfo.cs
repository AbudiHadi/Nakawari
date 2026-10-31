using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Nakawari.Services;

public sealed record GpuInfo(string Name, string Driver)
{
    /// <summary>RTX 40 desktop/laptop cards and the "Ada Generation" workstation cards.</summary>
    public bool IsAda => Regex.IsMatch(Name, @"RTX\s*40\d0|Ada Generation", RegexOptions.IgnoreCase);
}

public static class SystemInfo
{
    public static GpuInfo? QueryGpu()
    {
        try
        {
            var start = new ProcessStartInfo("nvidia-smi", "--query-gpu=name,driver_version --format=csv,noheader")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start)!;
            var line = process.StandardOutput.ReadLine();
            process.WaitForExit(5000);
            var parts = line?.Split(',', StringSplitOptions.TrimEntries);
            return parts is { Length: 2 } ? new GpuInfo(parts[0], parts[1]) : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    /// <summary>
    /// True when this exact game EXE is running. Matching by name alone is wrong: SCSKiller warms
    /// shaders by running same-named copies of games from its own staging folder. A process whose
    /// path can't be read is treated as the game (safer to wait than to write under a running game).
    /// </summary>
    public static bool IsRunning(string exePath)
    {
        var target = Path.GetFullPath(exePath);
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath));
        try
        {
            return processes.Any(p => ImagePath(p) is not { } path || path.Equals(target, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    /// <summary>Full path of a process's EXE via QueryFullProcessImageName (works for most elevated processes too).</summary>
    private static string? ImagePath(Process process)
    {
        const uint QueryLimitedInformation = 0x1000;
        var handle = OpenProcess(QueryLimitedInformation, false, process.Id);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new System.Text.StringBuilder(32768);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, System.Text.StringBuilder name, ref int size);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>Optional "update at Windows sign-in" via the per-user Run key (no admin, no scheduled task).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = Core.AppInfo.Name;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
    }

    /// <summary>Re-points an existing sign-in entry at the running exe (its path changes after install).</summary>
    public static void Refresh()
    {
        if (IsEnabled) Set(true);
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --update-all");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
