namespace Nakawari.Models;

/// <summary>What a scan learned about one game folder.</summary>
public sealed record ProbeResult(
    string? ExePath,
    IReadOnlyList<string> DlssgFiles,
    Version? DlssgVersion,
    Version? StreamlineVersion,
    string? AntiCheat,
    GameProfile Profile)
{
    public string? ExeDir => ExePath is null ? null : Path.GetDirectoryName(ExePath);

    /// <summary>Ships DLSS-G itself, or Streamline's DLSS-G plugin (which can load DLSS-G from the driver).</summary>
    public bool HasFrameGen => DlssgFiles.Count > 0 || StreamlineVersion is not null;
}
