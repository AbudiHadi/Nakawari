using Nakawari.Models;

namespace Nakawari.Services.Library;

public interface ILibrarySource
{
    string Store { get; }
    IReadOnlyList<GameEntry> Discover();
}
