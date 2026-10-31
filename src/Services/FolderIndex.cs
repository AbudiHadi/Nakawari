namespace Nakawari.Services;

/// <summary>One pass over a game folder that collects everything the probe needs.</summary>
public sealed class FolderIndex
{
    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true,
        // Unreal keeps Streamline under Engine\Plugins\Marketplace\nvidia\StreamlineCore\Binaries\ThirdParty\Win64.
        MaxRecursionDepth = 10,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>Streamline's frame-generation plugin. Some games ship only this and load DLSS-G from the driver.</summary>
    public const string StreamlinePlugin = "sl.dlss_g.dll";

    public List<string> Executables { get; } = [];
    public List<string> DlssgFiles { get; } = [];
    public List<string> StreamlineFiles { get; } = [];
    public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static FolderIndex Build(string root)
    {
        var index = new FolderIndex();
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", Walk))
        {
            var name = Path.GetFileName(entry);
            index.Names.Add(name);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) index.Executables.Add(entry);
            else if (name.Equals(DlssgService.FileName, StringComparison.OrdinalIgnoreCase)) index.DlssgFiles.Add(entry);
            else if (name.Equals(StreamlinePlugin, StringComparison.OrdinalIgnoreCase)) index.StreamlineFiles.Add(entry);
        }
        return index;
    }
}
