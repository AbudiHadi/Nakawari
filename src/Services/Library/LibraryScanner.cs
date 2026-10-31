using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services.Library;

/// <summary>Merges every store into one list, one entry per install folder.</summary>
public sealed class LibraryScanner(IEnumerable<ILibrarySource> sources)
{
    public IReadOnlyList<GameEntry> Scan()
    {
        var games = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            try
            {
                foreach (var game in source.Discover())
                    games.TryAdd(Path.GetFullPath(game.InstallDir).TrimEnd('\\'), game);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Log.Error($"{source.Store} library scan failed", ex);
            }
        }
        return games.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
