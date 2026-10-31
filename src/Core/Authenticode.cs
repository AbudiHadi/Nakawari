using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Nakawari.Core;

/// <summary>
/// Windows signature check (WinVerifyTrust) plus the signer's name. Every NVIDIA DLL Nakawari
/// downloads must pass this before it goes anywhere near a game folder.
/// </summary>
public static class Authenticode
{
    public static bool IsSignedBy(string path, string publisher)
    {
        if (!IsTrusted(path)) return false;
        try
        {
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is the documented way to read the Authenticode signer
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return cert.GetNameInfo(X509NameType.SimpleName, false).Contains(publisher, StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static bool IsTrusted(string path)
    {
        var fileInfo = new WinTrustFileInfo(path);
        var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
            var data = new WinTrustData(fileInfoPtr);
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"); // WINTRUST_ACTION_GENERIC_VERIFY_V2
            return WinVerifyTrust(IntPtr.Zero, action, ref data) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo(string path)
    {
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
        public string FilePath = path;
        public IntPtr FileHandle = IntPtr.Zero;
        public IntPtr KnownSubject = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData(IntPtr fileInfo)
    {
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustData>();
        public IntPtr PolicyCallbackData = IntPtr.Zero;
        public IntPtr SipClientData = IntPtr.Zero;
        public uint UiChoice = 2;            // WTD_UI_NONE
        public uint RevocationChecks = 0;    // WTD_REVOKE_NONE (works offline)
        public uint UnionChoice = 1;         // WTD_CHOICE_FILE
        public IntPtr FileInfo = fileInfo;
        public uint StateAction = 0;
        public IntPtr StateData = IntPtr.Zero;
        public IntPtr UrlReference = IntPtr.Zero;
        public uint ProvFlags = 0x10;        // WTD_CACHE_ONLY_URL_RETRIEVAL
        public uint UiContext = 0;
        public IntPtr SignatureSettings = IntPtr.Zero;
    }
}
