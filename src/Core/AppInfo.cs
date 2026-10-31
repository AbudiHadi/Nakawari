using System.Reflection;

namespace Nakawari.Core;

/// <summary>Product identity in one place, so the name and version never drift between UI, logs and HTTP.</summary>
public static class AppInfo
{
    public const string Name = "Nakawari";
    public const string Kanji = "中割り";
    public const string Publisher = "AbudiHadi";
    public const string Tagline = "In-between frames for every game";
    public const string RepoUrl = "https://github.com/AbudiHadi/Nakawari";

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static string UserAgent => $"{Name}/{Version} ({Publisher})";
}
