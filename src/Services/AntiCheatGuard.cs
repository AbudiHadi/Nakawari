namespace Nakawari.Services;

/// <summary>
/// The addon patches game memory, so kernel anti-cheat will flag it.
/// Any game that ships one of these is refused, never "tried".
/// </summary>
public sealed class AntiCheatGuard
{
    private static readonly (string Name, string[] Markers)[] Known =
    [
        ("Easy Anti-Cheat", ["EasyAntiCheat", "EasyAntiCheat_EOS", "start_protected_game.exe", "EasyAntiCheat_EOS_Setup.exe"]),
        ("BattlEye", ["BattlEye", "BEService_x64.exe", "BEClient_x64.dll"]),
        ("EA Anti-Cheat", ["EAAntiCheat.GameServiceLauncher.exe", "EAAntiCheat.Installer.exe"]),
        ("Riot Vanguard", ["vgk.sys"]),
        ("ACE", ["ACE-Base.sys", "AntiCheatExpert"]),
        ("nProtect GameGuard", ["GameGuard", "GameGuard.des"]),
        ("XIGNCODE3", ["xigncode", "x3.xem"]),
        ("mhyprot", ["mhyprot2.sys", "mhyprot3.sys"]),
    ];

    public string? Detect(FolderIndex index) =>
        Known.FirstOrDefault(k => k.Markers.Any(index.Names.Contains)).Name;
}
