using Nakawari.Core;
using Nakawari.Models;
using Nakawari.Services.Doctor;
using Nakawari.Services.Kit;
using Nakawari.Services.Library;

namespace Nakawari.Services;

/// <summary>Composition root shared by the window and the headless CLI modes.</summary>
public sealed class AppServices
{
    public AppServices()
    {
        Settings = AppSettings.Load();
        ScsKiller = new ScsKillerBridge();
        Profiles = new ProfileCatalog();
        Scanner = new LibraryScanner([new SteamLibrary(), new EpicLibrary(), new GogLibrary(), new ManualLibrary(Settings)]);
        Probe = new GameProbe(ScsKiller, new AntiCheatGuard(), Profiles);
        Addon = new AddonService(Settings);
        Dlssg = new DlssgService();
        Ledgers = new LedgerStore();
        Ceilings = new CeilingBook();
        var files = new GameFiles(Addon, new ReShadeService(), Ledgers);
        Kit = new NvidiaKit();
        KitInstaller = new KitInstaller(Kit, Ledgers);
        Installer = new MfgInstaller(files, Dlssg, Ledgers, Settings);
        Maintainer = new MfgMaintainer(files, Addon, Dlssg, KitInstaller, Profiles, Settings);
        Theme = new ThemeService(Settings.Theme);
        Covers = new CoverArtService();
        NewGames = new NewGameWatch();
        ShaderCache = new ShaderCache.ScsKillerCli();
        AppUpdates = new AppUpdateService();
    }

    public AppSettings Settings { get; }
    public ScsKillerBridge ScsKiller { get; }
    public ProfileCatalog Profiles { get; }
    public LibraryScanner Scanner { get; }
    public GameProbe Probe { get; }
    public AddonService Addon { get; }
    public DlssgService Dlssg { get; }
    public LedgerStore Ledgers { get; }
    public CeilingBook Ceilings { get; }
    public MfgInstaller Installer { get; }
    public MfgMaintainer Maintainer { get; }
    public NvidiaKit Kit { get; }
    public KitInstaller KitInstaller { get; }
    public ThemeService Theme { get; }
    public CoverArtService Covers { get; }
    public NewGameWatch NewGames { get; }
    public ShaderCache.ScsKillerCli ShaderCache { get; }
    public AppUpdateService AppUpdates { get; }

    /// <summary>Scans every store, probes each game, refreshes the reference DLSS-G and follows moved games.</summary>
    public IReadOnlyList<(GameEntry Game, ProbeResult Probe)> ScanAll()
    {
        var results = Scanner.Scan()
            .AsParallel().AsOrdered().WithDegreeOfParallelism(4)
            .Select(g => (Game: g, Probe: Probe.Probe(g)))
            .ToList();
        // The kit's 310.9.1 belongs with Streamline 2.14.1 only; never spread it as the general reference.
        Dlssg.Harvest(results.SelectMany(r => r.Probe.DlssgFiles).Where(f => !Kit.IsKitCopy(f)));
        Relink(results);
        return results;
    }

    /// <summary>Updates and repairs every enabled game from its ledger; scans only if a game seems to have moved.</summary>
    public async Task<IReadOnlyList<OpResult>> UpdateAllAsync()
    {
        await Profiles.LoadAsync();
        if (Ledgers.All().Any(l => !File.Exists(l.ExePath))) ScanAll();

        var results = new List<OpResult>();
        foreach (var ledger in Ledgers.All())
            results.Add(await Maintainer.UpdateAsync(ledger));
        return results;
    }

    /// <summary>
    /// The sign-in routine: update enabled games, then look for newly installed frame-gen games,
    /// enable them when the user opted in, and announce them with a Windows notification.
    /// </summary>
    public async Task<IReadOnlyList<OpResult>> SignInRoutineAsync()
    {
        var results = (await UpdateAllAsync()).ToList();
        foreach (var (game, probe) in NewGames.TakeNew(ScanAll()))
        {
            if (Settings.AutoEnableNewGames) results.Add(await Installer.EnableAsync(game, probe, 0));
            var action = Settings.AutoEnableNewGames ? "Nakawari enabled frame generation for it" : "Open Nakawari to enable it";
            await Notifier.ShowAsync($"New game ready: {game.Name}", $"It supports DLSS frame generation. {action}.");
        }
        return results;
    }

    private void Relink(IReadOnlyList<(GameEntry Game, ProbeResult Probe)> scan)
    {
        foreach (var ledger in Ledgers.All().Where(l => !File.Exists(l.ExePath)))
        {
            var match = scan.FirstOrDefault(r => r.Game.Id == ledger.GameId && r.Probe.ExePath is not null);
            if (match.Game is null) continue;

            ledger.MoveTo(match.Game.InstallDir, match.Probe.ExePath!);
            Ledgers.Save(ledger);
            Log.Info($"{ledger.GameName} moved to {match.Game.InstallDir}; Nakawari followed it");
        }
    }
}
