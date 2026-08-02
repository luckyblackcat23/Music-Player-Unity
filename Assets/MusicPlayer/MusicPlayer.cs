using System.Collections.Generic;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Events;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using Tools;
using MyBox;
using ManagedBass;
//using Kawazu;

//was planning for this to be used for global music
//although since its just using unity's audio source
//you could theoretically have a radio or some other external source place the audio
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    //Saved data
    public static SaveFile saveData = new("MusicPlayerData.txt");

    public enum LoopOptions { dontLoop, loop, loopSingle }

    [ReadOnly]
    [SerializeField]
    private LoopOptions loop_;
    public LoopOptions Loop
    {
        get
        {
            return loop_;
        }
        set
        {
            loop_ = value;

            if (loop_ == LoopOptions.loopSingle)
            {
                audioSource.loop = true;
            }
            else
            {
                audioSource.loop = false;
            }
        }
    }

    const float targetRMS = 0.12f;

    private SaveFloat userVolume = new("userVolume", saveData);
    public float UserVolume
    {
        get => userVolume;
        set
        {
            userVolume.Set(Mathf.Clamp01(value));

            UpdateVolume();
        }
    }

    [ReadOnly]
    public float playbackTime;

    [ReadOnly]
    public float clipLength = 1;

    [ReadOnly]
    public bool paused = true;

    [ReadOnly]
    public bool shuffle = true;

    public List<SongInfo> musicQueue = new();

    public static SongInfo[] cachedSongs;

    [ReadOnly] 
    public int currentSongIndex = 0;
    public SongInfo CurrentSong()
    {
        return musicQueue[currentSongIndex];
    }
    
    public AudioSource audioSource;

    [Space(10)]

    public UnityEvent OnSongChange;
    public UnityEvent OnSongEnd;
    public UnityEvent OnPlay;
    public UnityEvent OnPause;

    public bool useExternalSongs;

    [Tooltip("Start playing the music before the songs are done importing/downloading.")]
    [ConditionalField(nameof(useExternalSongs))]
    public bool playAllOnStart;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (playAllOnStart)
        {
            PlayAll();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (audioSource.clip)
            playbackTime = audioSource.time;

        if (!audioSource.isPlaying)
        {
            if (playbackTime >= clipLength)
            {
                OnSongEnd.Invoke();
                Debug.Log("playing next song");
                PlayNext();
            }
        }
    }

    [ButtonMethod]
    public void Pause()
    {
        paused = true;
        audioSource.Pause();
    }

    /// <summary>
    /// Starts playback. Plays the audioclip.
    /// </summary>
    [ButtonMethod]
    public void Play()
    {
        if (currentSongIndex >= musicQueue.Count)
        {
            Stop();
            return;
        }

        SongInfo currentSong = CurrentSong();

        if(audioSource.clip == null)
        {
            if(currentSong.audioClip == null)
            {
                StartCoroutine(GetClipFromFile(new FileInfo(currentSong.SongPath), clip =>
                {
                    if (clip == null)
                    {
                        playbackTime = 0;
                        return; // maybe log an error here?
                    }

                    paused = false;

                    if (clip != audioSource.clip)
                    {
                        playbackTime = 0;
                    }

                    if (clip == null || clip != audioSource.clip)
                    {
                        OnSongChange.Invoke();
                    }

                    audioSource.clip = clip;

                    if (currentSong.RMS <= 0)
                    {
                        currentSong.RMS = CalculateWindowedRMS(currentSong.SongPath);
                        Debug.Log("RMS = " + currentSong.RMS);
                    }

                    UpdateVolume();

                    audioSource.time = playbackTime;
                    audioSource.Play();

                    clipLength = clip.length;
                }));
            }
        }
        else
        {
            audioSource.UnPause();
            paused = false;
        }
    }

    [ButtonMethod]
    public void Stop()
    {
        audioSource.Stop();
        audioSource.clip = null;
    }


    [ButtonMethod]
    [Tooltip("If paused, unpause. If unpaused, pause.")]
    public void TogglePause()
    {
        if (audioSource.isPlaying)
            Pause();
        else
            Play();
    }

    /// <summary>
    /// Play the next song in the queue.
    /// Will EXPLAIN LATER
    /// </summary>
    [ButtonMethod]
    public void PlayNext()
    {
        if (currentSongIndex + 1 < musicQueue.Count)
        {
            currentSongIndex++;

            Stop();

            Play();
        }
        else
        {
            if (Loop != LoopOptions.dontLoop)
            {
                RestartQueue(shuffle);
            }
            else 
                Pause();
        }
    }

    /// <summary>
    /// Play the previous song in the queue.
    /// </summary>
    [ButtonMethod]
    public void PlayPrevious()
    {
        Stop();
        currentSongIndex--;

        if (currentSongIndex < 0)
        {
            currentSongIndex = 0;
        }

        Play();
    }

    /// <summary>
    /// Have this song play now, replacing the current song
    /// </summary>
    public void PlayNow(SongInfo song)
    {
        if (musicQueue.Count > 0)
            musicQueue.Insert(currentSongIndex, song);
        else
            musicQueue.Add(song);

        Stop();
        Play();
    }

    /// <summary>
    /// Play a song from the Queue using it's index
    /// </summary>
    public void PlayIndex(int index_)
    {
        Stop();
        currentSongIndex = index_;
        Play();
    }

    /// <summary>
    /// Have this song play next in the queue
    /// </summary>
    public void AddNext(SongInfo song)
    {
        musicQueue.Insert(currentSongIndex + 1, song);
    }

    public void AddEnd(SongInfo song)
    {
        musicQueue.Add(song);
    }

    public void AddStart(SongInfo song)
    {
        musicQueue.Insert(0, song);
    }

    public void AddPrevious(SongInfo song)
    {
        musicQueue.Insert(currentSongIndex - 1, song);
    }

    public void SetSongTime(float time)
    {
        audioSource.time = Mathf.Clamp(time, 0, (float)CurrentSong().Duration);
    }

    /// <summary>
    /// Restarts the queue.
    /// </summary>
    public void RestartQueue(bool shuffle_ = false)
    {
        currentSongIndex = 0;

        if (shuffle_)
        {
            //use another array to be shuffled (not shuffling the original array, in order to preserve user initialized order)
            SongInfo[] temp = new SongInfo[musicQueue.Count];
            musicQueue.CopyTo(temp, 0);

            System.Random rand = new();

            //shuffle the temporary array
            rand.Shuffle(temp);

            //set the musicQueue to the temporary shuffled queue
            musicQueue = temp.ToList();
        }
    }

    public void PlayAll(bool shuffle_ = false)
    {
        if (songsCached)
        {
            currentSongIndex = 0;

            if (shuffle_)
            {
                //use another array to be shuffled (not shuffling the original array, in order to preserve user initialized order)
                SongInfo[] temp = new SongInfo[cachedSongs.Length];
                cachedSongs.CopyTo(temp, 0);

                System.Random rand = new();

                //shuffle the temporary array
                rand.Shuffle(temp);

                //set the musicQueue to the temporary shuffled queue
                musicQueue = temp.ToList();
            }
            else
            {
                musicQueue = cachedSongs.ToList();
            }
        }
        else
        {
            Debug.LogWarning("Songs folder has not finished caching");
        }
    }

    [ButtonMethod]
    public void PlayTestPlaylist()
    {
        PlayPlaylist(Playlist.GetFromPath(Playlists()[0]));
    }

    [ButtonMethod]
    public static string[] Playlists()
    {
        DirectoryInfo info = new DirectoryInfo(Globals.PlaylistsPath);

        List<string> temp = new List<string>();

        foreach (FileInfo playlist in info.GetFiles().Where(file => supportedPlaylistExtensions.Contains(file.Extension.ToLower())).ToArray())
        {
            temp.Add(playlist.FullName);
        }

        return temp.ToArray();
    }

    public void PlayPlaylist(Playlist playlist)
    {
        musicQueue.Clear();

        currentSongIndex = 0;

        foreach (SongInfo song in playlist.GetSongs())
        {
            musicQueue.Add(song);
        }

        Stop();
        Play();
    }

    public void incrementLoop(bool direction = true)
    {
        if (direction)
        {
            if ((int)Loop < 2)
                Loop += 1;
            else
                Loop = 0;
        }
        else
        {
            if ((int)Loop > 0)
                Loop -= 1;
            else
                Loop = (LoopOptions)2;
        }
    }

    //file stuff
    //comment later


    public static bool songsCached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static async void InitializeLoadingFiles()
    {
        if (!Directory.Exists(Globals.PlaylistsPath))
        {
            Directory.CreateDirectory(Globals.PlaylistsPath);
        }

        DirectoryInfo info = new DirectoryInfo(Globals.SongsPath);

        FileInfo[] fileInfo = info.GetFiles().Where(file => supportedAudioExtensions.Contains(file.Extension.ToLower())).ToArray();

        List<Task> tasks = new();

        cachedSongs = new SongInfo[fileInfo.Length];

        for (int i = 0; i < fileInfo.Length; i++)
        {
            SongInfo song = new SongInfo(fileInfo[i].FullName);
            cachedSongs[i] = song;

            tasks.Add(Task.Run(() => 
            {
                song.GetSongSearchInfo();
            }));
        }

        await Task.WhenAll(tasks);

        songsCached = true;

        Debug.Log("Songs finished caching");
    }

    /// <summary>
    /// checks if a song exists is already cached
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool GetCachedSong(string path, out SongInfo song)
    {
        if (songsCached)
        {
            for (int i = 0; i < cachedSongs.Length; i++)
            {
                SongInfo cachedSong = cachedSongs[i];

                if (cachedSong.SongPath == path)
                {
                    song = cachedSong;
                    return true;
                }
            }

            song = null;
            return false;
        }
        else
        {
            Debug.Log("Cannot find cached song, songs have not finished caching.");

            song = null;
            return false;
        }
    } 

    //get mybox to work with this later
    public IEnumerator GetClipFromFile(FileInfo file, Action<AudioClip> callback)
    {
        string extension = file.Extension.ToLower();

        AudioType audioType;

        switch (extension)
        {
            case ".mp3":
                audioType = AudioType.MPEG;
                break;

            case ".ogg":
                audioType = AudioType.OGGVORBIS;
                break;

            case ".wav":
                audioType = AudioType.WAV;
                break;

            default:
                Debug.Log($"{file.Name} is not supported");
                callback?.Invoke(null);
                yield break;
        }

        Uri uri = new Uri(file.FullName);


        using UnityWebRequest request =
            UnityWebRequestMultimedia.GetAudioClip(
                uri.AbsoluteUri,
                audioType
            );

        

        DownloadHandlerAudioClip handler =
            (DownloadHandlerAudioClip)request.downloadHandler;

        // Keep this because you want faster playback startup
        handler.streamAudio = true;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            AudioClip clip = handler.audioClip;

            clip.name = file.Name;

            callback?.Invoke(clip);
        }
        else
        {
            Debug.LogError($"Failed to get file {file.Name}: {request.error}");
            callback?.Invoke(null);
        }
    }

    private static readonly HashSet<string> supportedAudioExtensions = new()
    {
        ".mp3",
        ".ogg",
        ".wav",
        ".m4a"
    };

    private static readonly HashSet<string> supportedPlaylistExtensions = new()
    {
        ".m3u",
        ".m3u8",
    };

    public void UpdateVolume()
    {
        float NormalisationGain = 1f;

        if (musicQueue.Count > 0)
        {
            NormalisationGain = targetRMS / CurrentSong().RMS;
        }

        audioSource.volume = userVolume * NormalisationGain;
    }

    public static float CalculateWindowedRMS(string path)
    {
        int stream = Bass.CreateStream(path, Flags: BassFlags.Decode | BassFlags.Float);

        if (stream == 0)
        {
            Debug.LogError($"Failed to open stream: {Bass.LastError}");
            return 0;
        }

        var info = Bass.ChannelGetInfo(stream);

        int channels = info.Channels;
        int sampleRate = info.Frequency;

        // 400 ms worth of samples
        int windowSize = Mathf.RoundToInt(sampleRate * 0.4f) * channels;

        float[] buffer = new float[8192];

        double totalEnergy = 0;
        int windows = 0;

        double windowEnergy = 0;
        int windowSamples = 0;

        while (true)
        {
            int bytesRead = Bass.ChannelGetData(stream, buffer, buffer.Length);

            if (bytesRead <= 0)
                break;

            int samplesRead = bytesRead / sizeof(float);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[i];

                windowEnergy += sample * sample;
                windowSamples++;

                if (windowSamples >= windowSize)
                {
                    double averageEnergy = windowEnergy / windowSamples;

                    // Ignore near silence
                    if (averageEnergy >= 0.0001)
                    {
                        totalEnergy += averageEnergy;
                        windows++;
                    }

                    windowEnergy = 0;
                    windowSamples = 0;
                }
            }
        }

        // Handle the final partial window
        if (windowSamples > 0)
        {
            double averageEnergy = windowEnergy / windowSamples;

            if (averageEnergy >= 0.0001)
            {
                totalEnergy += averageEnergy;
                windows++;
            }
        }

        Bass.StreamFree(stream);

        if (windows == 0)
            return 0;

        return Mathf.Sqrt((float)(totalEnergy / windows));
    }
}

//future improvement: Seperate loading romanized stuff and album covers
public class SongInfo
{
    public static bool DebugMessages = false;

    public bool MetaDataLoaded;
    public bool SearchMetaDataLoaded;

    public float RMS;

    public AudioClip audioClip;

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

    public SongInfo(string songPath, Texture2D albumCover = null, string title = "Unknown", string romanisedTitle = "Unknown", string artist = "Unknown", string romanisedArtist = "Unkown", string album = "Unknown", string romanisedAlbum = "Unkown", uint year = 0, string genre = "Unknown genre", double duration = 0)
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
        OnMetaDataLoaded = onMetaDataLoaded;

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

                // Album art (optional: lazy-load later)
                if (tfile.Tag.Pictures.Length > 0)
                {
                    try
                    {
                        //thank you chatGPT. please dont fuck me over later when i realise i never double checked this
                        using var ms = new MemoryStream(tfile.Tag.Pictures[0].Data.Data);

                        // Reset position just to be safe
                        ms.Position = 0;

                        // Read bytes
                        byte[] data = ms.ToArray();

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

            OnMetaDataLoaded.Invoke();
            MetaDataLoaded = true;

            SearchMetaDataLoaded = true;
        }
    }
}

/// <summary>
/// Stores and retrieves string values
/// Essentially the same as a SaveString. except it doesn't use the SavedString variable for compatability reasons
/// </summary>
public class SavePath : SaveVariable
{
    public static implicit operator string(SavePath obj) => obj.Get();

    public SavePath(string path, SaveFile saveFile = null) : base(path, saveFile) { }

    public string Get() => SavedName ?? string.Empty;

    public void Set(string v, bool UpdateOnChange = true)
    {
        SavedName = v ?? "";

        if (UpdateOnChange)
        {
            SaveFile.WriteFile();
        }
    }

    public override object GetAsObject() => Get();
    public override void SetFromObject(object v, bool UpdateOnChange = true) => Set((string)v, UpdateOnChange);

    public override string GetAsString() => SavedName;
    public override void SetFromString(string v, bool UpdateOnChange = true) => Set(v, UpdateOnChange);

    internal override string SavedString() { return SavedName; }
}

public class Playlist : SaveFile
{
    // figure out how to implement later
    //public SavePath PlaylistCoverPath;

    //create playlist
    public Playlist(string name, string savePath = null) : base(name, "m3u", savePath)
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

        playlist.UpdateCache();

        foreach (string line in playlist.cachedText)
        {
            if (line.StartsWith('#'))
            {
                //directive
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
            foreach(string line in cachedText)
            {
                if (line.StartsWith('#'))
                {
                    Directives.Add(line);
                }
            }

            using StreamWriter sw = new StreamWriter(SavedPath, false);

            foreach(string directive in Directives)
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
        foreach(SavePath songPath in Variables)
        {
            if(songPath == song.SongPath)
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

//probably an easier way to do this rather than importing a new function
/* moved to a different script
static class RandomExtensions
{
    public static void Shuffle<T>(this System.Random rng, T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = rng.Next(n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }
}
*/