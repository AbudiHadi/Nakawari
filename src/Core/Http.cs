namespace Nakawari.Core;

public static class Http
{
    public static HttpClient Client { get; } = Create();

    /// <summary>Downloads to a temp file, checks the size, then moves into place.</summary>
    public static async Task DownloadAsync(string url, string target, long? expectedSize = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".download";
        using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(temp);
            await source.CopyToAsync(file);
        }

        if (expectedSize is { } size && new FileInfo(temp).Length != size)
        {
            File.Delete(temp);
            throw new IOException($"Download size mismatch for {Path.GetFileName(target)}.");
        }
        File.Move(temp, target, overwrite: true);
    }

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        return client;
    }
}
