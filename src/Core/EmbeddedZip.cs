using System.IO.Compression;

namespace Nakawari.Core;

/// <summary>
/// Reads a ZIP appended to an executable (the ReShade installer ships its DLLs this way).
/// The archive start is found from the end-of-central-directory record, the same way 7-Zip does it.
/// </summary>
public static class EmbeddedZip
{
    private static readonly byte[] EndOfCentralDirectory = [0x50, 0x4B, 0x05, 0x06];

    public static void ExtractEntry(string container, string entryName, string target)
    {
        var bytes = File.ReadAllBytes(container);
        var eocd = LastIndexOf(bytes, EndOfCentralDirectory);
        if (eocd < 0) throw new InvalidDataException($"{Path.GetFileName(container)} has no embedded archive.");

        var directorySize = BitConverter.ToUInt32(bytes, eocd + 12);
        var directoryOffset = BitConverter.ToUInt32(bytes, eocd + 16);
        var archiveStart = (int)(eocd - directorySize - directoryOffset);

        using var stream = new MemoryStream(bytes, archiveStart, bytes.Length - archiveStart, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = zip.GetEntry(entryName)
            ?? throw new InvalidDataException($"{entryName} not found in {Path.GetFileName(container)}.");

        var temp = target + ".tmp";
        entry.ExtractToFile(temp, overwrite: true);
        File.Move(temp, target, overwrite: true);
    }

    private static int LastIndexOf(byte[] data, byte[] pattern)
    {
        for (var i = data.Length - pattern.Length; i >= 0; i--)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        }
        return -1;
    }
}
