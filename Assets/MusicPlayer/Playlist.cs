using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Playlist
{
    /// <summary>
    /// Name of the playlist.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Full path to the playlist file.
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// Songs contained in this playlist, in playlist order.
    /// </summary>
    public List<string> Songs { get; } = new();

    /// <summary>
    /// M3U directives/comments that were present in the file.
    /// These are preserved when saving.
    /// </summary>
    private readonly List<string> directives = new();

    /// <summary>
    /// Creates a playlist from an existing file.
    /// </summary>
    public Playlist(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);

        Load();
    }

    /// <summary>
    /// Creates a new playlist.
    /// </summary>
    public Playlist(string name, string directory, bool save = true)
    {
        Name = name;
        Path = System.IO.Path.Combine(directory, name + ".m3u");

        if (save)
            Save();
    }

    /// <summary>
    /// Creates a playlist and adds the supplied songs.
    /// </summary>
    public static Playlist CreatePlaylist(
        string name,
        List<SongInfo> songs,
        string directory)
    {
        Playlist playlist = new Playlist(name, directory, false);

        playlist.AddSongs(songs, false);
        playlist.Save();

        return playlist;
    }

    /// <summary>
    /// Loads a playlist from a path.
    /// </summary>
    public static Playlist GetFromPath(string path)
    {
        return PlaylistManager.GetPlaylist(path);
    }

    /// <summary>
    /// Loads the playlist from disk.
    /// </summary>
    public void Load()
    {
        Songs.Clear();
        directives.Clear();

        if (!File.Exists(Path))
            return;

        foreach (string line in File.ReadLines(Path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("#"))
            {
                directives.Add(line);
                continue;
            }

            Songs.Add(line);
        }
    }

    /// <summary>
    /// Saves the playlist to disk.
    /// </summary>
    public void Save()
    {
        string directory = System.IO.Path.GetDirectoryName(Path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using StreamWriter writer = new StreamWriter(Path, false);

        foreach (string directive in directives)
            writer.WriteLine(directive);

        foreach (string song in Songs)
            writer.WriteLine(song);
    }

    /// <summary>
    /// Adds a song to the playlist.
    /// </summary>
    public void AddSong(SongInfo song, bool save = true)
    {
        if (song == null)
            return;

        AddSong(song.SongPath, save);
    }

    /// <summary>
    /// Adds a song path to the playlist.
    /// </summary>
    public void AddSong(string songPath, bool save = true)
    {
        if (string.IsNullOrEmpty(songPath))
            return;

        if (Songs.Contains(songPath))
            return;

        Songs.Add(songPath);

        if (save)
            Save();
    }

    /// <summary>
    /// Adds multiple songs to the playlist.
    /// </summary>
    public void AddSongs(List<SongInfo> songs, bool save = true)
    {
        if (songs == null)
            return;

        foreach (SongInfo song in songs)
            AddSong(song, false);

        if (save)
            Save();
    }

    /// <summary>
    /// Removes a song from the playlist.
    /// </summary>
    public void RemoveSong(SongInfo song, bool save = true)
    {
        if (song == null)
            return;

        RemoveSong(song.SongPath, save);
    }

    /// <summary>
    /// Removes a song path from the playlist.
    /// </summary>
    public void RemoveSong(string songPath, bool save = true)
    {
        if (!Songs.Remove(songPath))
            return;

        if (save)
            Save();
    }

    /// <summary>
    /// Removes a song at a specific index.
    /// </summary>
    public void RemoveSongAt(int index, bool save = true)
    {
        if (index < 0 || index >= Songs.Count)
            return;

        Songs.RemoveAt(index);

        if (save)
            Save();
    }

    /// <summary>
    /// Moves a song from one position to another.
    /// </summary>
    public void MoveSong(int from, int to, bool save = true)
    {
        if (from < 0 || from >= Songs.Count)
            return;

        if (to < 0 || to >= Songs.Count)
            return;

        if (from == to)
            return;

        string song = Songs[from];

        Songs.RemoveAt(from);
        Songs.Insert(to, song);

        if (save)
            Save();
    }

    /// <summary>
    /// Gets the SongInfo objects for all songs that are currently cached.
    /// </summary>
    public List<SongInfo> GetSongs()
    {
        List<SongInfo> songs = new();

        foreach (string path in Songs)
        {
            if (MusicPlayer.GetCachedSong(path, out SongInfo song))
                songs.Add(song);
        }

        return songs;
    }

    /// <summary>
    /// Gets the SongInfo objects for all songs in the playlist.
    /// </summary>
    public List<SongInfo> GetSongs(bool loadMissing)
    {
        List<SongInfo> songs = new();

        foreach (string path in Songs)
        {
            if (MusicPlayer.GetCachedSong(path, out SongInfo song))
            {
                songs.Add(song);
            }
            else if (loadMissing && File.Exists(path))
            {
                SongInfo newSong = new SongInfo(path);
                songs.Add(newSong);
            }
        }

        return songs;
    }

    /// <summary>
    /// Checks whether the playlist contains a song.
    /// </summary>
    public bool Contains(SongInfo song)
    {
        return song != null && Songs.Contains(song.SongPath);
    }

    /// <summary>
    /// Checks whether the playlist contains a song path.
    /// </summary>
    public bool Contains(string songPath)
    {
        return Songs.Contains(songPath);
    }

    /// <summary>
    /// Adds an M3U directive/comment.
    /// </summary>
    public void AddDirective(string directive, bool save = true)
    {
        if (string.IsNullOrEmpty(directive))
            return;

        if (!directive.StartsWith("#"))
            directive = "#" + directive;

        directives.Add(directive);

        if (save)
            Save();
    }

    /// <summary>
    /// Removes an M3U directive/comment.
    /// </summary>
    public void RemoveDirective(string directive, bool save = true)
    {
        if (!directives.Remove(directive))
            return;

        if (save)
            Save();
    }
}


public static class PlaylistManager
{
    private static readonly List<Playlist> playlists = new();

    /// <summary>
    /// Gets a playlist from the cache, or loads it from disk.
    /// </summary>
    public static Playlist GetPlaylist(string path)
    {
        Playlist playlist = playlists.FirstOrDefault(
            p => string.Equals(
                p.Path,
                path,
                StringComparison.OrdinalIgnoreCase));

        if (playlist != null)
            return playlist;

        playlist = new Playlist(path);
        playlists.Add(playlist);

        return playlist;
    }

    /// <summary>
    /// Creates a new playlist and adds it to the cache.
    /// </summary>
    public static Playlist CreatePlaylist(
        string name,
        string directory)
    {
        Playlist playlist = new Playlist(name, directory);

        playlists.Add(playlist);

        return playlist;
    }

    /// <summary>
    /// Creates a new playlist with songs and adds it to the cache.
    /// </summary>
    public static Playlist CreatePlaylist(
        string name,
        List<SongInfo> songs,
        string directory)
    {
        Playlist playlist = Playlist.CreatePlaylist(
            name,
            songs,
            directory);

        playlists.Add(playlist);

        return playlist;
    }

    /// <summary>
    /// Returns all currently loaded playlists.
    /// </summary>
    public static IReadOnlyList<Playlist> Playlists => playlists;

    /// <summary>
    /// Removes a playlist from the manager.
    /// Does not delete the file from disk.
    /// </summary>
    public static bool RemovePlaylist(Playlist playlist)
    {
        if (playlist == null)
            return false;

        return playlists.Remove(playlist);
    }

    /// <summary>
    /// Saves every loaded playlist.
    /// </summary>
    public static void SaveAll()
    {
        foreach (Playlist playlist in playlists)
            playlist.Save();
    }

    /// <summary>
    /// Reloads every loaded playlist from disk.
    /// </summary>
    public static void LoadAll()
    {
        foreach (Playlist playlist in playlists)
            playlist.Load();
    }

    /// <summary>
    /// Clears the playlist cache.
    /// Does not delete any playlist files.
    /// </summary>
    public static void Clear()
    {
        playlists.Clear();
    }
}