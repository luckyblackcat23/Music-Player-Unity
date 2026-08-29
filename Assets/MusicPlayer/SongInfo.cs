using UnityEngine;
using System;

//future improvement: Seperate loading romanized stuff and album covers
[Serializable]
public class SongInfo
{
    public static bool DebugMessages = false;

    public bool MetaDataLoaded;
    public bool SearchMetaDataLoaded;

    public float RMS;

    public string SongPath;

    private Texture2D _albumCover;
    public Texture2D AlbumCover
    {
        get
        {
            if (_albumCover != null)
                return _albumCover;
            else
                return null; // return default album cover
        }
        set
        {
            _albumCover = value;
        }
    }

    public string Title;
    public string RomanisedTitle;

    public string Artist;
    public string RomanisedArtist;

    public string Album;
    public string RomanisedAlbum;

    public uint Year;

    public string Genre;

    public double Duration;

    public SongInfo(string songPath, Texture2D albumCover = null, string title = "Unknown", string romanisedTitle = "Unknown", string artist = "Unknown", string romanisedArtist = "Unknown", string album = "Unknown", string romanisedAlbum = "Unknown", uint year = 0, string genre = "Unknown genre", double duration = 0)
    {
        SongPath = songPath;

        AlbumCover = albumCover;

        Title = title;
        RomanisedTitle = romanisedTitle;

        Artist = artist;
        RomanisedArtist = artist;

        Album = album;
        RomanisedAlbum = album;

        Year = year;

        Genre = genre;

        Duration = duration;

        OnMetaDataLoaded += onMetaDataLoaded;
    }

    public event Action OnMetaDataLoaded;

    public void onMetaDataLoaded()
    {
        if (DebugMessages)
            Debug.Log("meta data loaded for " + Title);
    }

    /// <summary>
    /// gets the lightweight fileinfo. quicker to retrieve and can be loaded without causing many performance issues
    /// </summary>
    /// <param name="overwrite">overwrite existing values, if there are any</param>
    public void GetSongSearchInfo(bool overwrite = false)
    {
        if (!SearchMetaDataLoaded || overwrite)
        {
            try
            {
                using var tfile = TagLib.File.Create(SongPath);

                Title = tfile.Tag.Title;
                Artist = tfile.Tag.FirstPerformer;
                Album = tfile.Tag.Album;
                Year = tfile.Tag.Year;
                Genre = tfile.Tag.JoinedGenres;
                Duration = tfile.Properties.Duration.TotalSeconds;

                SearchMetaDataLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing {SongPath}: {ex.Message}");
            }
        }
        else
            Debug.LogWarning("Search MetaData has already been loaded");
    }

    /// <summary>
    /// load the metadata of the audio file
    /// </summary>
    /// <param name="overwrite">overwrite existing values, if there are any</param>
    public void GetSongInfo(bool overwrite = false)
    {
        if (!MetaDataLoaded || overwrite)
        {
            //using var converter = new KawazuConverter();

            try
            {
                using var tfile = TagLib.File.Create(SongPath);

                if (!SearchMetaDataLoaded || overwrite)
                {
                    Title = tfile.Tag.Title;
                    Artist = tfile.Tag.FirstPerformer;
                    Album = tfile.Tag.Album;
                    Year = tfile.Tag.Year;
                    Genre = tfile.Tag.JoinedGenres;
                    Duration = tfile.Properties.Duration.TotalSeconds;
                }

                /* maybe re add later. too much of a headache to fix now

                // Romanisation only if needed
                if (!string.IsNullOrWhiteSpace(Title))
                    RomanisedTitle = await converter.Convert(Title, To.Romaji);
                if (!string.IsNullOrWhiteSpace(Artist))
                    RomanisedArtist = await converter.Convert(Artist, To.Romaji);
                if (!string.IsNullOrWhiteSpace(Album))
                    RomanisedAlbum = await converter.Convert(Album, To.Romaji);

                */

                if (tfile.Tag.Pictures.Length > 0)
                {
                    try
                    {
                        // Read bytes
                        byte[] data = tfile.Tag.Pictures[0].Data.Data;

                        // Create an empty texture (size doesn't matter; LoadImage replaces it)
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                        // Load image data
                        tex.LoadImage(data); // Automatically resizes the texture

                        AlbumCover = tex;
                    }
                    //not sure if this will ever be needed. just re-using old code. 
                    catch (Exception ex)
                    {
                        Debug.LogError("Error while loading album image for " + Title + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing {SongPath}: {ex.Message}");
            }

            OnMetaDataLoaded?.Invoke();
            MetaDataLoaded = true;

            SearchMetaDataLoaded = true;
        }
    }

    public void DisposeAlbumCover()
    {
        if (AlbumCover != null)
        {
            UnityEngine.Object.Destroy(AlbumCover);
            AlbumCover = null;

            MetaDataLoaded = false;
        }
    }
}