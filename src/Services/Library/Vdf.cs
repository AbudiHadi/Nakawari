using System.Text.RegularExpressions;

namespace Nakawari.Services.Library;

/// <summary>Just enough of Valve's KeyValues text format to read library folders and app manifests.</summary>
public static partial class Vdf
{
    [GeneratedRegex("^\\s*\"([^\"]+)\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex Pair();

    public static IEnumerable<(string Key, string Value)> Pairs(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var match = Pair().Match(line);
            if (match.Success) yield return (match.Groups[1].Value, match.Groups[2].Value.Replace(@"\\", @"\"));
        }
    }

    public static string? First(IEnumerable<(string Key, string Value)> pairs, string key) =>
        pairs.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}
