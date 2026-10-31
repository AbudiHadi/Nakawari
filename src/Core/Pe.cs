using System.Diagnostics;
using System.Security.Cryptography;

namespace Nakawari.Core;

/// <summary>Read-only facts about a DLL/EXE on disk.</summary>
public static class Pe
{
    public static string? ProductName(string path)
    {
        try { return FileVersionInfo.GetVersionInfo(path).ProductName?.Trim(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Numeric file version (FileVersion strings use commas in NVIDIA DLLs, so parts are read directly).</summary>
    public static Version? FileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string Sha256Text(string text) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToUpperInvariant())));

    public static bool SameContent(string a, string b) =>
        File.Exists(a) && File.Exists(b) && new FileInfo(a).Length == new FileInfo(b).Length && Sha256(a) == Sha256(b);
}
