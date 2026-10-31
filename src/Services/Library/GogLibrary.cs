using Microsoft.Win32;
using Nakawari.Models;

namespace Nakawari.Services.Library;

public sealed class GogLibrary : ILibrarySource
{
    public string Store => "GOG";

    public IReadOnlyList<GameEntry> Discover()
    {
        using var games = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
        if (games is null) return [];

        var result = new List<GameEntry>();
        foreach (var id in games.GetSubKeyNames())
        {
            using var key = games.OpenSubKey(id);
            var name = key?.GetValue("gameName") as string;
            var dir = key?.GetValue("path") as string;
            if (name is null || dir is null || !Directory.Exists(dir)) continue;
            result.Add(new GameEntry($"gog_{id}", name, Store, dir, key!.GetValue("exe") as string));
        }
        return result;
    }
}
