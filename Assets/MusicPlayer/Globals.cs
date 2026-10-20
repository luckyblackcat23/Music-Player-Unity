using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

public static class Globals
{
    //public const Int32 BUFFER_SIZE = 512; // Unmodifiable
    //public static String FILE_NAME = "Output.txt"; // Modifiable
    //public static readonly String CODE_PREFIX = "US-"; // Unmodifiable

    public const string SaveFolderName = "MusicPlayer";
    public static readonly string SaveFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SaveFolderName);

    public static string SongsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
    public static string PlaylistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Playlists");

    public const string GameName = "MusicPlayer";

    public static void OpenSavePath()
    {
        Process.Start(SaveFolderPath);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    public static IntPtr GetUnityWindow()
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning("Active window is the editor window");
#endif

        return Process.GetCurrentProcess().MainWindowHandle;
    }
}
