using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services.Library;

/// <summary>Folders the user added with "Add game folder".</summary>
public sealed class ManualLibrary(AppSettings settings) : ILibrarySource
{
    public string Store => "Manual";

    public IReadOnlyList<GameEntry> Discover() =>
        settings.ManualFolders.Where(Directory.Exists).Select(EntryFor).ToList();

    public static GameEntry EntryFor(string dir) =>
        new($"manual_{Pe.Sha256Text(dir)[..12]}", Path.GetFileName(dir.TrimEnd('\\')), "Manual", dir);
}
