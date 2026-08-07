using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using MyBox;

public class Playlist : SaveFile
{
    // figure out how to implement later
    //public SavePath PlaylistCoverPath;

    public string playlistName;

    //create playlist
    public Playlist(string fileName, string savePath = null) : base(fileName, "m3u", savePath)
    {

    }

    public static Playlist CreatePlaylist(string name, List<SongInfo> songs)
    {
        List<SavePath> songPaths = new();

        Playlist playlist = new Playlist(name);

        foreach (SongInfo s in songs)
        {
            playlist.AddSong(s);
        }

        return playlist;
    }

    /// <summary>
    /// Get a playlist from the specified path
    /// Will return cached Playlist from the save manager if it already exists in memory
    /// </summary>
    /// <param name="path">Full path of the song</param>
    /// <returns></returns>
    public static Playlist GetFromPath(string path)
    {
        Playlist playlist = (Playlist)SaveManager.GetFile(path);

        if (playlist != null)
        {
            Debug.LogWarning("File already exists in the SaveManager, returning SaveManager's");
            return playlist;
        }

        FileInfo playlistFile = new FileInfo(path);

        playlist = new Playlist(playlistFile.Name, path);

        playlist.playlistName = playlistFile.Name.RemoveEnd(playlistFile.Extension);

        playlist.UpdateCache();

        foreach (string line in playlist.cachedText)
        {
            if (line.StartsWith('#'))
            {
                //directives
                if (line == "#EXTM3U")
                {

                }
                else
                {

                }
            }
            else
            {
                if (File.Exists(line))
                {
                    new SavePath(line, playlist);
                }
            }
        }

        SaveManager.RegisterFile(playlist);

        return playlist;
    }

    internal override void WriteFile(bool updateCacheAfter = true)
    {
        UpdateCache();

        try
        {
            List<string> Directives = new();

            //ensure we dont overwrite directives already in the file
            foreach (string line in cachedText)
            {
                if (line.StartsWith('#'))
                {
                    Directives.Add(line);
                }
            }

            using StreamWriter sw = new StreamWriter(SavedPath, false);

            foreach (string directive in Directives)
            {
                sw.WriteLine(directive);
            }

            foreach (var variable in Variables)
            {
                sw.WriteLine(variable.SavedString());
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"[SaveFile] Failed to save {SavedName}: {ex}");
        }

        if (updateCacheAfter)
            UpdateCache();
    }

    public override List<string> WriteVariables()
    {
        UpdateCache();

        foreach (SaveVariable variable in Variables)
        {
            foreach (string line in cachedText)
            {
                if (line.StartsWith('#'))
                {
                    // directives
                }
                else
                {
                    if (variable.GetType() == typeof(SavePath))
                    {
                        if (line == variable.SavedString())
                            variable.SetFromString(line, false);
                    }
                    else
                    {
                        if (line.StartsWith($"{variable.SavedName}="))
                            variable.SetFromString(line.Remove(0, $"{variable.SavedName}=".Length), false);
                    }
                }
            }
        }

        return cachedText;
    }

    public void AddSong(SongInfo song)
    {
        foreach (SavePath songPath in Variables)
        {
            if (songPath == song.SongPath)
            {
                //song already exists in the playlist. add confirmation message before adding
                return;
            }
        }

        SavePath newSong = new SavePath(song.Title, this);
        newSong.Set(song.SongPath);

        Variables.Add(newSong);
    }

    /*
    public async Task<Texture2D> GetPlaylistCover()
    {
        if (!File.Exists(PlaylistCoverPath.Value == null ? PlaylistCoverPath.Value : ""))
        {
            Debug.Log((PlaylistCoverPath.Value ?? "empty") + " does not have a cover");
            return GetSongs()[0].AlbumCover;
        }
        else
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture("file://" + PlaylistCoverPath))
            {
                await uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(uwr.error);
                }
                else
                {
                    // Get downloaded asset bundle
                    return DownloadHandlerTexture.GetContent(uwr);
                }
            }
        }

        return null;
    }
    */

    public SongInfo[] GetSongs()
    {
        SongInfo[] songs = new SongInfo[Variables.Count];

        for (int i = 0; i < Variables.Count; i++)
        {
            SongInfo song;

            if (MusicPlayer.GetCachedSong(Variables[i].GetAsString(), out song))
            {
                songs[i] = song;
            }
        }

        return songs;
    }
}