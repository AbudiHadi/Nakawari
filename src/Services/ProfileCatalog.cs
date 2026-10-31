using System.Text.Json;
using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Game fixes that can change without an app release: the built-in list, overridden by
/// profiles.json from the Nakawari GitHub repo (fetched at most once a day, cached for offline use).
/// </summary>
public sealed class ProfileCatalog
{
    private const string RemoteUrl = "https://raw.githubusercontent.com/AbudiHadi/Nakawari/main/profiles.json";
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static readonly GameProfile[] BuiltIn =
    [
        new("Hogwarts Legacy", HdrMode: 2, Note: "HDR fix mode applied automatically"),
        new("Jusant", HdrMode: 2, Note: "HDR fix mode applied automatically"),
        new("Mafia: The Old Country", HdrMode: 2, Note: "HDR fix mode applied automatically"),
        new("Indiana Jones", Note: "Vulkan game: needs the addon's Vulkan launch option (experimental)"),
        new("DOOM: The Dark Ages", Note: "Vulkan game: needs the addon's Vulkan launch option (experimental)"),
        new("Monster Hunter Wilds", Note: "Restart the game once after shader compilation"),
    ];

    private static string CachePath => Path.Combine(AppPaths.Root, "profiles.json");
    private GameProfile[] _profiles = BuiltIn;

    public GameProfile For(string gameName) =>
        _profiles.FirstOrDefault(p => p.Match.Length > 0 && gameName.Contains(p.Match, StringComparison.OrdinalIgnoreCase))
        ?? GameProfile.Default;

    /// <summary>Loads the cached copy, then refreshes it from GitHub when it is older than a day.</summary>
    public async Task LoadAsync()
    {
        Merge(ReadCache());
        if (File.Exists(CachePath) && DateTime.Now - File.GetLastWriteTime(CachePath) < RefreshEvery) return;

        try
        {
            var json = await Http.Client.GetStringAsync(RemoteUrl);
            var remote = JsonSerializer.Deserialize<GameProfile[]>(json, Json);
            if (remote is null) return;
            File.WriteAllText(CachePath, json);
            Merge(remote);
            Log.Info($"Game fixes updated: {remote.Length} profile(s) from GitHub");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // Offline or not published yet: the built-in list and cache still apply.
        }
    }

    private static GameProfile[] ReadCache()
    {
        try { return File.Exists(CachePath) ? JsonSerializer.Deserialize<GameProfile[]>(File.ReadAllText(CachePath), Json) ?? [] : []; }
        catch (Exception ex) when (ex is JsonException or IOException) { return []; }
    }

    /// <summary>Remote entries win over built-in ones with the same Match.</summary>
    private void Merge(IEnumerable<GameProfile> remote)
    {
        var byMatch = BuiltIn.ToDictionary(p => p.Match, StringComparer.OrdinalIgnoreCase);
        foreach (var profile in remote.Where(p => !string.IsNullOrWhiteSpace(p.Match))) byMatch[profile.Match] = profile;
        _profiles = [.. byMatch.Values];
    }
}
