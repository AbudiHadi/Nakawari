using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace Nakawari.Core;

public sealed record RemoteZipEntry(string Name, long CompressedSize, long Size, int Method, long LocalHeaderOffset);

/// <summary>
/// Reads single files out of a large ZIP on a web server with HTTP range requests, so the
/// 276 MB Streamline SDK costs ~5 MB to pull the handful of runtime DLLs Nakawari needs.
/// Supports stored and deflated entries (no ZIP64; the SDK archive doesn't need it).
/// </summary>
public sealed class RemoteZip(string url)
{
    private const int TailBytes = 256 * 1024;
    private const int LocalHeaderSlack = 1024;

    public async Task<IReadOnlyList<RemoteZipEntry>> ListAsync()
    {
        var (tail, tailStart) = await ReadTailAsync();
        var eocd = LastIndexOf(tail, [0x50, 0x4B, 0x05, 0x06]);
        if (eocd < 0) throw new InvalidDataException("Not a ZIP archive (no end of central directory).");

        var count = BitConverter.ToUInt16(tail, eocd + 10);
        var directorySize = BitConverter.ToUInt32(tail, eocd + 12);
        var directoryOffset = BitConverter.ToUInt32(tail, eocd + 16);
        var position = (int)(directoryOffset - tailStart);
        if (position < 0 || position + directorySize > tail.Length)
            throw new InvalidDataException("ZIP central directory lies outside the downloaded tail.");

        var entries = new List<RemoteZipEntry>(count);
        for (var i = 0; i < count && BitConverter.ToUInt32(tail, position) == 0x02014B50; i++)
        {
            var method = BitConverter.ToUInt16(tail, position + 10);
            var compressed = BitConverter.ToUInt32(tail, position + 20);
            var size = BitConverter.ToUInt32(tail, position + 24);
            var nameLength = BitConverter.ToUInt16(tail, position + 28);
            var extraLength = BitConverter.ToUInt16(tail, position + 30);
            var commentLength = BitConverter.ToUInt16(tail, position + 32);
            var localOffset = BitConverter.ToUInt32(tail, position + 42);
            var name = Encoding.UTF8.GetString(tail, position + 46, nameLength);
            entries.Add(new RemoteZipEntry(name, compressed, size, method, localOffset));
            position += 46 + nameLength + extraLength + commentLength;
        }
        return entries;
    }

    public async Task ExtractAsync(RemoteZipEntry entry, string target)
    {
        var chunk = await ReadRangeAsync(entry.LocalHeaderOffset, entry.LocalHeaderOffset + 30 + LocalHeaderSlack + entry.CompressedSize - 1);
        if (BitConverter.ToUInt32(chunk, 0) != 0x04034B50) throw new InvalidDataException($"Bad local header for {entry.Name}.");

        var dataStart = 30 + BitConverter.ToUInt16(chunk, 26) + BitConverter.ToUInt16(chunk, 28);
        using var data = new MemoryStream(chunk, dataStart, (int)entry.CompressedSize, writable: false);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".tmp";
        await using (var output = File.Create(temp))
        {
            if (entry.Method == 0) await data.CopyToAsync(output);
            else if (entry.Method == 8) await using (var inflate = new DeflateStream(data, CompressionMode.Decompress)) await inflate.CopyToAsync(output);
            else throw new InvalidDataException($"Unsupported ZIP method {entry.Method} for {entry.Name}.");
        }

        if (new FileInfo(temp).Length != entry.Size)
        {
            File.Delete(temp);
            throw new InvalidDataException($"{entry.Name} came out the wrong size.");
        }
        File.Move(temp, target, overwrite: true);
    }

    /// <summary>
    /// GitHub's asset CDN refuses suffix ranges ("bytes=-N"), so the size is learned from a
    /// one-byte range first and the tail is then asked for with explicit start and end.
    /// </summary>
    private async Task<(byte[] Data, long Start)> ReadTailAsync()
    {
        var total = await LengthAsync();
        var start = Math.Max(0, total - TailBytes);
        return (await ReadRangeAsync(start, total - 1), start);
    }

    private async Task<long> LengthAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await Http.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentRange?.Length
            ?? throw new InvalidDataException("The server does not support partial downloads.");
    }

    private async Task<byte[]> ReadRangeAsync(long from, long to)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(from, to);
        using var response = await Http.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static int LastIndexOf(byte[] data, byte[] pattern)
    {
        for (var i = data.Length - pattern.Length; i >= 0; i--)
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        return -1;
    }
}
