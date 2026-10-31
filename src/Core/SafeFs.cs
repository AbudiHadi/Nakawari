using System.Text;

namespace Nakawari.Core;

/// <summary>
/// The only way this app writes into a game folder. It refuses to touch SCSKiller's files:
/// SCSKiller owns d3d12.dll and scskiller.* and arms its recorder against them.
/// </summary>
public static class SafeFs
{
    private static readonly HashSet<string> ScsKillerFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "d3d12.dll", "steam_appid.txt",
    };

    public static bool IsProtected(string path)
    {
        var name = Path.GetFileName(path);
        return ScsKillerFiles.Contains(name) || name.StartsWith("scskiller", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Copies through a temp file so a crash never leaves a half-written DLL.</summary>
    public static void Copy(string source, string target)
    {
        Guard(target);
        var temp = target + ".mfgtmp";
        File.Copy(source, temp, overwrite: true);
        File.Move(temp, target, overwrite: true);
    }

    public static void Delete(string path)
    {
        Guard(path);
        if (File.Exists(path)) File.Delete(path);
    }

    public static void WriteText(string path, string text, Encoding encoding)
    {
        Guard(path);
        File.WriteAllText(path, text, encoding);
    }

    private static void Guard(string path)
    {
        if (IsProtected(path))
            throw new InvalidOperationException($"Refusing to touch SCSKiller file '{Path.GetFileName(path)}'.");
    }
}
