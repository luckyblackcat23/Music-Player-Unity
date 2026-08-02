using System;
using System.Diagnostics;
using System.IO;

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
}
