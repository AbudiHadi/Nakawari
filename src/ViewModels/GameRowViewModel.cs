using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nakawari.Models;
using Nakawari.Services;
using Nakawari.Services.Doctor;
using Nakawari.Services.ShaderCache;

namespace Nakawari.ViewModels;

public enum RowState { Off, Ready, Enabled, Working, Attention, UpdateAvailable, Blocked }

/// <summary>One game in the list: what it is, how healthy it is, and the single action it offers.</summary>
public sealed class GameRowViewModel : ObservableObject
{
    private const int AbsoluteMax = 6;   // Streamline's own hard ceiling: 5 generated frames

    private readonly AppServices _services;
    private readonly Action<OpResult> _report;
    private ProbeResult _probe;
    private Ledger? _ledger;
    private GameHealth? _health;
    private string? _blocker;
    private string? _latestTag;
    private int _multiplier;
    private int? _max;
    private ImageSource? _cover;
    private ShaderStatus? _shader;
    private CancellationTokenSource? _compile;

    public GameRowViewModel(GameEntry game, ProbeResult probe, AppServices services, Action<OpResult> report)
    {
        Game = game;
        _services = services;
        _report = report;
        _probe = probe;
        Load();
        ToggleCommand = new AsyncCommand(ToggleAsync, () => IsEnabled || _blocker is null);
        CycleMultiplierCommand = AsyncCommand.From(CycleMultiplier);
        CompileShadersCommand = new AsyncCommand(CompileShadersAsync, () => ShaderId is not null);
        RecordShadersCommand = new AsyncCommand(RecordShadersAsync, () => ShaderId is not null && _compile is null);
        OpenScsKillerCommand = AsyncCommand.From(_services.ShaderCache.OpenApp, () => _services.ShaderCache.IsInstalled);
        ToggleKitCommand = new AsyncCommand(ToggleKitAsync, () => KitAvailable);
        PacingCommands = Enum.GetValues<PacingPreset>()
            .Select(p => new PacingOption(Pacing.Label(p), AsyncCommand.From(() => SetPacing(p), () => IsEnabled)))
            .ToList();
        _ = LoadCoverAsync();
        if (IsRelevant) _ = LoadShaderAsync();
    }

    public GameEntry Game { get; }
    public ICommand ToggleCommand { get; }
    public ICommand CycleMultiplierCommand { get; }
    public ICommand CompileShadersCommand { get; }
    public ICommand RecordShadersCommand { get; }
    public ICommand OpenScsKillerCommand { get; }
    public ICommand ToggleKitCommand { get; }
    public IReadOnlyList<PacingOption> PacingCommands { get; }
    public string PacingText => $"Pacing: {Pacing.Label(_ledger?.Pacing ?? PacingPreset.GameDefault)} ▾";

    /// <summary>The 50-series kit applies to enabled games that ship their own Streamline runtime.</summary>
    public bool KitAvailable => IsEnabled && _probe.StreamlineVersion is not null;
    public bool KitOn => _ledger?.KitVersion is not null;
    public string KitText => KitOn
        ? $"⚡ 50-series kit on: Streamline {_ledger!.KitVersion}, Dynamic MFG (click to remove)"
        : "⚡ Try the 50-series kit: Dynamic MFG, up to 6x";

    public ImageSource? Cover { get => _cover; private set => Set(ref _cover, value); }
    public string Name => Game.Name;
    public string Store => Game.Store;
    public string DlssgText => _probe.DlssgVersion?.ToString() ?? (_probe.StreamlineVersion is not null ? "driver" : "-");
    public bool IsEnabled => _ledger is not null;
    public bool IsRelevant => _probe.HasFrameGen || IsEnabled;
    private bool HandInstalled => !IsEnabled && _probe.ExeDir is { } dir && File.Exists(Path.Combine(dir, AddonService.FileName));

    private string? ShaderId => _services.ShaderCache.IsInstalled ? ScsKillerCli.IdFor(Game) : null;

    public string ScsKillerText => _compile is not null ? "Compiling…" : _shader?.Label ?? ScsKillerBridge.StateOf(_probe.ExeDir) switch
    {
        ScsState.Armed => "Recording",
        ScsState.Recorder => "Recorder on",
        _ => "-",
    };

    public string ShaderTip => _shader?.Detail
        ?? (_services.ShaderCache.IsInstalled ? "Shader cache by SCSKiller. Click for actions." : "Install SCSKiller to pre-compile shaders and stop stutter.");

    public string CompileText => _compile is null ? "Compile shaders now" : "Stop compiling";

    public RowState State => this switch
    {
        { IsEnabled: true } when _latestTag is not null && _ledger!.PinnedTag is null && _ledger.AddonTag != _latestTag => RowState.UpdateAvailable,
        { IsEnabled: true } when _health?.State is HealthState.Problem or HealthState.ReShadeNotLoading => RowState.Attention,
        { IsEnabled: true } when _health?.State is HealthState.Working => RowState.Working,
        { IsEnabled: true } => RowState.Enabled,
        _ when _blocker is not null && _probe.HasFrameGen => RowState.Blocked,
        _ when _blocker is not null => RowState.Off,
        _ => RowState.Ready,
    };

    public string StateText => State switch
    {
        RowState.UpdateAvailable => $"Update  {_ledger!.AddonTag} → {_latestTag}",
        RowState.Working => "Working  ✓",
        RowState.Attention => "Needs attention",
        RowState.Enabled when _ledger!.PinnedTag is not null => $"Held at {_ledger.PinnedTag}",
        RowState.Enabled when _health?.State is HealthState.NotRunYet => "On  ·  play once to check",
        RowState.Enabled => "On",
        RowState.Ready when HandInstalled => "Installed by hand",
        RowState.Ready => "Ready",
        _ => _blocker!,
    };

    public string StateTip => _health?.Detail ?? StateText;

    public string? Note => this switch
    {
        { State: RowState.Attention } => _health!.Detail,
        _ when HandInstalled => "Click Manage to let Nakawari keep it updated (your files are backed up)",
        _ when _max is { } max && _multiplier > max => $"This game allows up to {max}x; {_multiplier}x falls back to the game's choice",
        _ => _probe.Profile.Note,
    };

    public bool HasNote => Note is not null;
    public string ActionText => IsEnabled ? "Disable" : HandInstalled ? "Manage" : "Enable";

    public string MultiplierText => _multiplier switch
    {
        MfgConfig.Dynamic => "Dynamic",
        0 => "Game menu",
        var m when m == _max => $"{m}x  max",
        var m => $"{m}x",
    };

    public string MultiplierTip => _max is { } max
        ? $"This game's frame generation allows up to {max}x. Click to cycle: Game menu, 2x … {max}x."
        : "This game's maximum isn't known yet (play once and Nakawari learns it). Click to cycle: Game menu, 2x … 6x.";

    public void SetLatestTag(string? tag)
    {
        _latestTag = tag;
        OnAllChanged();
    }

    private void Load()
    {
        _ledger = _services.Ledgers.Load(Game.Id);
        _blocker = _services.Installer.Blocker(_probe);
        _health = _ledger is null ? null : HealthReader.Read(_ledger.ExeDir, _ledger.LastChangedAt);
        if (_health?.MaxMultiplier is { } learned) _services.Ceilings.Learn(_probe.StreamlineVersion, learned);
        _max = _health?.MaxMultiplier ?? _services.Ceilings.Predict(_probe.StreamlineVersion);
        _multiplier = _ledger?.Multiplier ?? (_probe.ExeDir is { } dir ? MfgConfig.ReadMultiplier(dir) : 0);
    }

    private async Task ToggleAsync()
    {
        var result = IsEnabled
            ? _services.Installer.Disable(_ledger!)
            : await _services.Installer.EnableAsync(Game, _probe, _multiplier);
        _report(result);

        _probe = await Task.Run(() => _services.Probe.Probe(Game));
        Load();
        OnAllChanged();
    }

    /// <summary>Asks first (it swaps NVIDIA runtime DLLs), then installs or removes the 50-series kit.</summary>
    private async Task ToggleKitAsync()
    {
        if (KitOn)
        {
            _report(_services.KitInstaller.Remove(_ledger!));
        }
        else
        {
            var answer = System.Windows.MessageBox.Show(
                $"Install the 50-series kit into {Name}?\n\n" +
                "• Downloads NVIDIA's official Streamline 2.14.1 runtime with DLSS-G 310.9.1 from NVIDIA's GitHub (about 5 MB). " +
                "Every file must carry NVIDIA's signature.\n" +
                "• Replaces this game's Streamline DLLs. The originals are backed up, and \"click to remove\" puts them back.\n" +
                "• Turns on NVIDIA Dynamic MFG (the multiplier adapts to your refresh rate).\n\n" +
                "Experimental: some games may not start with a newer Streamline. If it crashes twice, Nakawari removes the kit by itself.\n\n" +
                "In the NVIDIA App, set \"DLSS Override - Frame Generation\" for this game to \"Use the 3D application setting\".",
                "50-series kit", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Information);
            if (answer != System.Windows.MessageBoxResult.OK) return;

            _report(OpResult.Success($"{Name}: fetching NVIDIA's kit…"));
            try { _report(await _services.KitInstaller.InstallAsync(_ledger!)); }
            catch (Exception ex) when (GameFiles.IsExpected(ex))
            {
                _report(OpResult.Fail($"{Name}: kit not installed - {GameFiles.Explain(ex)}"));
            }
        }

        _probe = await Task.Run(() => _services.Probe.Probe(Game));
        Load();
        OnAllChanged();
    }

    private void SetPacing(PacingPreset preset)
    {
        _report(_services.Installer.SetPacing(_ledger!, preset));
        OnPropertyChanged(nameof(PacingText));
    }

    private void CycleMultiplier()
    {
        var fixedSteps = MfgConfig.Multipliers.Where(m => m <= (_max ?? AbsoluteMax));
        var options = (KitOn ? fixedSteps.Prepend(MfgConfig.Dynamic) : fixedSteps).ToArray();
        _multiplier = options[(Array.IndexOf(options, _multiplier) + 1) % options.Length];
        if (IsEnabled) _report(_services.Installer.SetMultiplier(_ledger!, _multiplier, _probe.Profile));
        OnAllChanged();
    }

    private async Task LoadShaderAsync()
    {
        if (ShaderId is not { } id) return;
        _shader = await _services.ShaderCache.StatusAsync(id);
        OnPropertyChanged(nameof(ScsKillerText));
        OnPropertyChanged(nameof(ShaderTip));
    }

    /// <summary>Compile in the background at idle priority; a second click stops it.</summary>
    private async Task CompileShadersAsync()
    {
        if (_compile is not null) { _compile.Cancel(); return; }
        if (_probe.ExePath is { } exe && SystemInfo.IsRunning(exe)) { _report(OpResult.Fail($"{Name}: close the game before compiling shaders")); return; }

        _compile = new CancellationTokenSource();
        OnAllChanged();
        try { _report(await _services.ShaderCache.CompileAsync(ShaderId!, Name, _compile.Token)); }
        finally
        {
            _compile.Dispose();
            _compile = null;
            await LoadShaderAsync();
            OnAllChanged();
        }
    }

    private async Task RecordShadersAsync()
    {
        _report(await _services.ShaderCache.RecordAsync(ShaderId!, Name));
        await LoadShaderAsync();
    }

    private async Task LoadCoverAsync()
    {
        var path = await _services.Covers.FindAsync(Game);
        if (path is null) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 240;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Cover = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or System.Runtime.InteropServices.COMException)
        {
            // a broken cache file just means no cover
        }
    }
}
