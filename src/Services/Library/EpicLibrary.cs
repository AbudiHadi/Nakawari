using System.Text.Json;
using Nakawari.Models;

namespace Nakawari.Services.Library;

public sealed class EpicLibrary : ILibrarySource
{
    private static readonly string ManifestDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public string Store => "Epic";

    public IReadOnlyList<GameEntry> Discover()
    {
        if (!Directory.Exists(ManifestDir)) return [];
        return Directory.EnumerateFiles(ManifestDir, "*.item")
            .Select(Read)
            .OfType<GameEntry>()
            .ToList();
    }

    private GameEntry? Read(string manifest)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = doc.RootElement;
            var name = Text(root, "DisplayName");
            var dir = Text(root, "InstallLocation");
            var app = Text(root, "AppName");
            if (name is null || dir is null || app is null || !Directory.Exists(dir)) return null;

            var exe = Text(root, "LaunchExecutable");
            var exePath = exe is null ? null : Path.Combine(dir, exe);
            return new GameEntry($"epic_{app}", name, Store, dir, exePath);
        }
        catch (JsonException) { return null; }
    }

    private static string? Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
