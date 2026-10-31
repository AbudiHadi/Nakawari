using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Win32;
using Nakawari.Core;
using Nakawari.Models;
using Nakawari.Services;

namespace Nakawari.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private List<GameRowViewModel> _all = [];
    private string _status = "Scanning your libraries...";
    private string _gpuText = "Checking GPU...";
    private string _addonText = "Checking for addon updates...";
    private bool _gpuWarning;
    private bool _busy;
    private string? _latestTag;
    private List<string> _freshNames = [];
    private string _narration = "Hello! I'm looking across your game libraries for games that can fly with in-between frames...";

    public MainViewModel(AppServices services)
    {
        _services = services;
        ToggleThemeCommand = AsyncCommand.From(ToggleTheme);
        ReportProblemCommand = AsyncCommand.From(ReportProblem);
        UpdateAppCommand = new AsyncCommand(_services.AppUpdates.ApplyAndRestartAsync, () => !Busy && _services.AppUpdates.PendingVersion is not null);
        AboutCommand = AsyncCommand.From(() => new AboutWindow(_services) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog());
        RefreshCommand = new AsyncCommand(RefreshAsync, () => !Busy);
        UpdateAllCommand = new AsyncCommand(UpdateAllAsync, () => !Busy);
        AddFolderCommand = new AsyncCommand(AddFolderAsync, () => !Busy);
        OpenDlssgFolderCommand = AsyncCommand.From(() => OpenFolder(DlssgService.CacheFolder));
        OpenLogCommand = AsyncCommand.From(() => OpenFolder(AppPaths.Root));
    }

    public ObservableCollection<GameRowViewModel> Rows { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand OpenDlssgFolderCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ReportProblemCommand { get; }
    public ICommand UpdateAppCommand { get; }

    public bool AppUpdateAvailable => _services.AppUpdates.PendingVersion is not null;
    public string AppUpdateText => $"Update Nakawari to {_services.AppUpdates.PendingVersion}";
    public ICommand AboutCommand { get; }

    public string Tagline => AppInfo.Tagline;
    public string VersionText => $"  |  v{AppInfo.Version}  |  by {AppInfo.Publisher}";
    public string Narration { get => _narration; private set => Set(ref _narration, value); }

    public string ThemeGlyph => _services.Theme.Choice switch
    {
        ThemeChoice.Day => "☀",
        ThemeChoice.Night => "☾",
        _ => "◐",
    };

    public string ThemeTip => $"Look: {_services.Theme.Choice} (click to change)";

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string GpuText { get => _gpuText; private set => Set(ref _gpuText, value); }
    public bool GpuWarning { get => _gpuWarning; private set => Set(ref _gpuWarning, value); }
    public string AddonText { get => _addonText; private set => Set(ref _addonText, value); }
    public bool Busy { get => _busy; private set => Set(ref _busy, value); }

    public string ScsKillerText => _services.ScsKiller.IsInstalled
        ? $"Shader cache: SCSKiller {_services.ShaderCache.Version}{(_services.ShaderCache.IsKnownVersion ? "" : " (newer than tested; actions use the SCSKiller app)")}"
        : "Shader cache: SCSKiller not installed";

    public bool ShowAllGames
    {
        get => _services.Settings.ShowAllGames;
        set { _services.Settings.ShowAllGames = value; _services.Settings.Save(); ApplyFilter(); OnPropertyChanged(); }
    }

    public bool UpgradeDlssg
    {
        get => _services.Settings.UpgradeDlssg;
        set { _services.Settings.UpgradeDlssg = value; _services.Settings.Save(); OnPropertyChanged(); }
    }

    public bool AutoEnableNewGames
    {
        get => _services.Settings.AutoEnableNewGames;
        set { _services.Settings.AutoEnableNewGames = value; _services.Settings.Save(); OnPropertyChanged(); }
    }

    public bool UpdateAtSignIn
    {
        get => StartupRegistration.IsEnabled;
        set { StartupRegistration.Set(value); OnPropertyChanged(); }
    }

    public async Task InitializeAsync()
    {
        var gpu = await Task.Run(SystemInfo.QueryGpu);
        GpuText = gpu is null ? "No NVIDIA GPU detected" : $"{gpu.Name}  |  driver {gpu.Driver}";
        GpuWarning = gpu is null || !gpu.IsAda;
        if (GpuWarning) GpuText += "  |  MFG Unlock is made for RTX 40 (Ada)";

        await RefreshAsync();
        await CheckLatestAsync();
        StartupRegistration.Refresh();   // keeps the sign-in entry pointing at this (installed) exe
        if (await _services.AppUpdates.CheckAsync() is not null)
        {
            OnPropertyChanged(nameof(AppUpdateAvailable));
            OnPropertyChanged(nameof(AppUpdateText));
        }
    }

    private async Task RefreshAsync()
    {
        Busy = true;
        Status = "Scanning your libraries...";
        try
        {
            await _services.Profiles.LoadAsync();
            var probed = await Task.Run(_services.ScanAll);
            _freshNames = _services.NewGames.TakeNew(probed).Select(r => r.Game.Name).ToList();

            _all = probed.Select(p => new GameRowViewModel(p.Game, p.Probe, _services, Report)).ToList();
            foreach (var row in _all) row.SetLatestTag(_latestTag);
            ApplyFilter();

            var withFrameGen = _all.Count(r => r.IsRelevant);
            var dlssg = _services.Dlssg.CachedVersion?.ToString() ?? "none";
            Status = $"{_all.Count} games found, {withFrameGen} with DLSS Frame Generation. Reference DLSS-G: {dlssg}";
            Narrate();
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task CheckLatestAsync()
    {
        try
        {
            var latest = await _services.Addon.LatestAsync();
            _latestTag = latest.Tag;
            AddonText = $"MFG Unlock {latest.Tag} (latest)";
            foreach (var row in _all) row.SetLatestTag(_latestTag);
            Narrate();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            AddonText = _services.Addon.CachedTag is { } tag ? $"MFG Unlock {tag} (offline)" : "MFG Unlock: offline";
        }
    }

    private async Task UpdateAllAsync()
    {
        Busy = true;
        Status = "Updating every enabled game...";
        try
        {
            var results = await _services.UpdateAllAsync();
            Status = results.Count == 0
                ? "No games enabled yet"
                : string.Join("   |   ", results.Select(r => r.Message));
        }
        finally
        {
            Busy = false;
        }
        await RefreshAsync();
    }

    private async Task AddFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Pick the game's install folder" };
        if (dialog.ShowDialog() != true) return;

        if (!_services.Settings.ManualFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
        {
            _services.Settings.ManualFolders.Add(dialog.FolderName);
            _services.Settings.Save();
        }
        ShowAllGames = true;
        await RefreshAsync();
    }

    private void Narrate()
    {
        var enabled = _all.Count(r => r.IsEnabled);
        var working = _all.Count(r => r.State == RowState.Working);
        var attention = _all.FirstOrDefault(r => r.State == RowState.Attention);
        var ready = _all.Count(r => r.State == RowState.Ready);
        var updates = _all.Count(r => r.State == RowState.UpdateAvailable);
        Narration = (enabled, ready, updates) switch
        {
            _ when _freshNames.Count > 0 => $"Something new on the wind: {string.Join(", ", _freshNames)} can use frame generation. Press Enable!",
            _ when attention is not null => $"{attention.Name} needs a look: {attention.Note}",
            (_, _, > 0) => $"A new addon ({_latestTag}) arrived on the wind! Press Update all and {updates} game(s) catch it.",
            (0, 0, _) => "I couldn't find a game with DLSS Frame Generation yet. Installed one somewhere else? Try Add game folder.",
            (0, _, _) => $"{ready} game(s) are ready to fly. Press Enable on one and pick a multiplier.",
            _ when working == enabled => $"All {enabled} game(s) are flying with in-between frames. I checked their last sessions!",
            _ => $"{enabled} game(s) are set up, {working} confirmed working. Play the others once and I'll check them.",
        };
    }

    private void ReportProblem()
    {
        var gpu = GpuText;
        var zip = ProblemReport.Create(_services, gpu);
        ProblemReport.Open(zip, gpu);
        Status = $"Report saved to your Desktop ({Path.GetFileName(zip)}). Attach it to the GitHub issue that just opened.";
    }

    private void ToggleTheme()
    {
        _services.Theme.Choice = _services.Theme.Choice switch
        {
            ThemeChoice.Auto => ThemeChoice.Day,
            ThemeChoice.Day => ThemeChoice.Night,
            _ => ThemeChoice.Auto,
        };
        _services.Settings.Theme = _services.Theme.Choice;
        _services.Settings.Save();
        OnPropertyChanged(nameof(ThemeGlyph));
        OnPropertyChanged(nameof(ThemeTip));
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _all.Where(r => ShowAllGames || r.IsRelevant))
            Rows.Add(row);
    }

    private void Report(OpResult result) => Status = (result.Ok ? "OK  " : "!  ") + result.Message;

    private static void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
}
