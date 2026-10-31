using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

public sealed class LedgerStore
{
    public Ledger? Load(string gameId) => JsonStore.Load<Ledger>(PathFor(gameId));

    public IReadOnlyList<Ledger> All() =>
        Directory.EnumerateFiles(AppPaths.Ledgers, "*.json")
            .Select(JsonStore.Load<Ledger>)
            .OfType<Ledger>()
            .ToList();

    public void Save(Ledger ledger) => JsonStore.Save(PathFor(ledger.GameId), ledger);

    public void Delete(string gameId) => File.Delete(PathFor(gameId));

    private static string PathFor(string gameId)
    {
        var safe = string.Concat(gameId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(AppPaths.Ledgers, safe + ".json");
    }
}
