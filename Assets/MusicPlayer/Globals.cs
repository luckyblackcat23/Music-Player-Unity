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

    public static readonly string PlaylistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SaveFolderName, "Playlists");
    public static string SongsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), GameVariables.gameNameChangeLater, "Songs"); //CHANGE THIS LATER

    public const string gameNameChangeLater = "GameNameHereReplaceLater"; //DO NOT FORGET TO CHANGE THIS
    public readonly static string GamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), gameNameChangeLater);

    public static void OpenSavePath()
    {
        Process.Start(Globals.SaveFolderPath);
    }
}
