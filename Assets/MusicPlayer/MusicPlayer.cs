using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine;
using ManagedBass;
using System.Linq;
using System.IO;
using R128Net;
using System;
using Tools;
using MyBox;
//using Kawazu;

public static class MusicPlayer
{
    //Saved data
    public static SaveFile saveData = new("MusicPlayerData");

    public enum LoopOptions { dontLoop, loop, loopSingle }

    private static LoopOptions loop_;
    public static LoopOptions Loop
    {
        get => loop_;
        set => loop_ = value;
    }

    const float targetLUFS = -14f;

    private static SaveFloat userVolumeSave = new("userVolume", saveData);
    public static float UserVolume
    {
        get => userVolumeSave;
        set
        {
            userVolumeSave.Set(Mathf.Clamp01(value));

            UpdateVolume();
        }
    }

    public static SaveFloat playbackTime = new SaveFloat("PlaybackTime", saveData);

    public static float clipLength = 1;

    public static bool paused = true;

    public static bool shuffle = true;

    public static SaveBool AudioNormalisationEnabled = new SaveBool("AudioNormalisationEnabled", saveData, true);

    public static List<SongInfo> musicQueue = new();

    public static SongInfo[] cachedSongs;

    public static SaveString currentSongPath = new SaveString("LastLoadedSongPath", saveData);

    public static int currentSongIndex = 0;
    public static SongInfo CurrentSong()
    {
        if (musicQueue == null || musicQueue?.Count == 0)
            return null;
        else
            return musicQueue[currentSongIndex];
    }

    [Space(10)]

    public static UnityEvent OnSongChange;
    public static UnityEvent OnSongEnd;
    public static UnityEvent OnPlay;
    public static UnityEvent OnPause;
    public static UnityEvent OnSongShuffle;

    public static bool useExternalSongs;

    private static MusicPlayerRunner runner;
    private static bool runtimeInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        if (runner != null)
            return;

        GameObject obj = new GameObject("MusicPlayer");
        UnityEngine.Object.DontDestroyOnLoad(obj);
        runner = obj.AddComponent<MusicPlayerRunner>();
    }

    // Called by MusicPlayerRunner.Awake because Unity cannot invoke Awake on a static class.
    internal static void InitializeRuntime()
    {
        if (runtimeInitialized)
            return;

        runtimeInitialized = true;

        OnSongChange ??= new UnityEvent();
        OnSongEnd ??= new UnityEvent();
        OnPlay ??= new UnityEvent();
        OnPause ??= new UnityEvent();
        OnSongShuffle ??= new UnityEvent();

        OnSongChange.AddListener(() =>
        {
            SongInfo song = CurrentSong();
            if (song != null)
                currentSongPath.Set(song.SongPath);
        });

        if (string.IsNullOrEmpty(currentSongPath))
            playbackTime.Set(0);
        else
        {
            if (GetCachedSong(currentSongPath, out SongInfo lastSong))
            {
                lastSong.GetSongInfo();
                PlayNow(lastSong, false);
            }
            else
            {
                playbackTime.Set(0);
            }
        }

        PlaylistDirectoryNode = FileNode.BuildTree(Globals.PlaylistsPath);
    }

    internal static void ShutdownRuntime()
    {
        FreeCurrentStream();
        runtimeInitialized = false;
        runner = null;
    }

    static bool songEnding = true;

    public static void Update()
    {
        if (currentStream != 0)
        {
            playbackTime.Set(GetSongTime());

            if (streamEnded)
            {
                streamEnded = false;
                HandleSongEnded();
            }
        }
    }

    private static SyncProcedure endSyncProcedure;

    private static void SetupEndSync()
    {
        if (currentStream == 0)
            return;

        // Keep the delegate alive for as long as Bass may call it.
        endSyncProcedure = EndSyncCallback;
        Bass.ChannelSetSync(currentStream, SyncFlags.End, 0, endSyncProcedure);
    }

    private static void EndSyncCallback(int handle, int channel, int data, IntPtr user)
    {
        streamEnded = true;
    }

    /// <summary>
    /// Starts playback. Plays the audioclip.
    /// </summary>
    public static void Play(bool startPlayback = true)
    {
        if (currentSongIndex >= musicQueue.Count)
        {
            Stop();
            return;
        }

        SongInfo currentSong = CurrentSong();

        if (!currentSong.MetaDataLoaded)
            currentSong.GetSongInfo();


        if (currentStream == 0)
        {
            if (!LoadStream(currentSong.SongPath))
            {
                playbackTime.Set(0);
                return;
            }

            playbackTime.Set(0);

            OnSongChange?.Invoke();

            if (AudioNormalisationEnabled)
            {
                if (!currentSong.LUFSCalculated)
                {
                    currentSong.LUFSCalculated = true;
                    currentSong.LUFS = CalculateLUFS(currentSong.SongPath);

                    Debug.Log("LUFS = " + currentSong.LUFS);
                }
            }

            UpdateVolume();

            clipLength = (float)Bass.ChannelBytes2Seconds(currentStream, Bass.ChannelGetLength(currentStream));
        }


        // Restore saved position
        SetSongTime(playbackTime);

        if (!startPlayback)
            return;

        paused = false;
        streamEnded = false;

        Bass.ChannelPlay(currentStream);

        OnPlay?.Invoke();
    }

    public static void Stop()
    {
        FreeCurrentStream();

        paused = true;
        streamEnded = false;
        playbackTime.Set(0);
    }

    public static void Pause()
    {
        if (currentStream == 0)
            return;

        paused = true;

        Bass.ChannelPause(currentStream);

        playbackTime.Set(GetSongTime());

        OnPause?.Invoke();
    }

    private static void HandleSongEnded()
    {
        OnSongEnd?.Invoke();

        if (Loop == LoopOptions.loopSingle)
        {
            SetSongTime(0);
            Bass.ChannelPlay(currentStream);
            return;
        }

        PlayNext();
    }

    public static float GetSongTime()
    {
        if (currentStream == 0)
            return 0f;

        long position = Bass.ChannelGetPosition(currentStream);

        return (float)Bass.ChannelBytes2Seconds(currentStream, position);
    }

    [Tooltip("If paused, unpause. If unpaused, pause.")]
    public static void TogglePause()
    {
        if (currentStream == 0)
        {
            Play();
            return;
        }

        if (paused)
            Play();
        else
            Pause();
    }

    /// <summary>
    /// Play the next song in the queue.
    /// whole lot more but will EXPLAIN LATER
    /// </summary>
    public static void PlayNext(bool startPlayback = true)
    {
        if (currentSongIndex + 1 < musicQueue.Count)
        {
            currentSongIndex++;

            Stop();

            Play(startPlayback);
        }
        else
        {
            if (Loop != LoopOptions.dontLoop && musicQueue.Count > 0)
            {
                RestartQueue(shuffle);
                Play(startPlayback);
            }
            else
            {
                Pause();
            }
        }
    }

    /// <summary>
    /// Play the previous song in the queue.
    /// </summary>
    public static void PlayPrevious(bool startPlayback = true)
    {
        Stop();
        currentSongIndex--;

        if (currentSongIndex < 0)
        {
            currentSongIndex = 0;
        }

        Play(startPlayback);
    }

    /// <summary>
    /// Have this song play now, replacing the current song
    /// </summary>
    public static void PlayNow(SongInfo song, bool startPlayback = true)
    {
        if (musicQueue.Count > 0)
            musicQueue.Insert(currentSongIndex, song);
        else
            musicQueue.Add(song);

        Stop();

        Play(startPlayback);
    }

    /// <summary>
    /// Play a song from the Queue using it's index
    /// </summary>
    public static void PlayIndex(int index_, bool startPlayback = true)
    {
        Stop();
        currentSongIndex = index_;

        Play(startPlayback);
    }

    /// <summary>
    /// Have this song play next in the queue
    /// </summary>
    public static void AddNext(SongInfo song)
    {
        musicQueue.Insert(currentSongIndex + 1, song);
    }

    public static void AddEnd(SongInfo song)
    {
        musicQueue.Add(song);
    }

    public static void AddStart(SongInfo song)
    {
        musicQueue.Insert(0, song);
    }

    public static void AddPrevious(SongInfo song)
    {
        musicQueue.Insert(currentSongIndex - 1, song);
    }

    public static void SetSongTime(float time)
    {
        if (currentStream == 0)
            return;

        time = Mathf.Clamp(time, 0f, clipLength);

        long position = Bass.ChannelSeconds2Bytes(currentStream, time);

        Bass.ChannelSetPosition(currentStream, position);

        playbackTime.Set(time);
    }

    /// <summary>
    /// Restarts the queue.
    /// </summary>
    public static void RestartQueue(bool shuffle_ = false)
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

    public static void PlayAll(bool shuffle_ = false)
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
            Debug.LogError("Attempted to play all songs when songs have not finished caching");
        }
    }

    public static FileNode PlaylistDirectoryNode;

    public static void PlayPlaylist(Playlist playlist)
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

    public static void AddPlaylistNext(Playlist playlist)
    {
        foreach (SongInfo song in playlist.GetSongs())
        {
            musicQueue.Add(song);
        }
    }

    public static void IncrementLoop(bool direction = true)
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

    public static void ShuffleQueue()
    {
        if (musicQueue.Count > 0)
        {
            SongInfo currentSong = musicQueue[currentSongIndex];

            musicQueue.Shuffle();

            currentSongIndex = musicQueue.IndexOf(currentSong);

            OnSongShuffle.Invoke();
        }
    }

    //file stuff
    //comment later


    public static bool songsCached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static async void InitializeLoadingFiles()
    {
        if (!Directory.Exists(Globals.SongsPath))
        {
            Directory.CreateDirectory(Globals.SongsPath);
        }

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

        Debug.Log($"Finished caching {tasks.Count} songs");
    }

    /// <summary>
    /// checks if a song exists is already cached
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool GetCachedSong(string path, out SongInfo song)
    {
        song = null;

        if (!songsCached)
        {
            Debug.Log("Cannot find cached song, songs have not finished caching.");
            return false;
        }

        string targetPath = Path.GetFullPath(path);

        for (int i = 0; i < cachedSongs.Length; i++)
        {
            SongInfo cachedSong = cachedSongs[i];

            if (cachedSong == null || string.IsNullOrEmpty(cachedSong.SongPath))
                continue;

            string cachedPath = Path.GetFullPath(cachedSong.SongPath);

            if (string.Equals(cachedPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                song = cachedSong;
                return true;
            }
        }

        return false;
    }

    private static int currentStream;

    private static bool streamPlaying;
    private static bool streamEnded;

    private static bool LoadStream(string path)
    {
        FreeCurrentStream();

        currentStream = Bass.CreateStream(
            path,
            0,
            0,
            BassFlags.Float
        );

        if (currentStream == 0)
        {
            Debug.LogError(
                $"Failed to create Bass stream for '{path}': {Bass.LastError}"
            );

            return false;
        }

        streamEnded = false;
        SetupEndSync();
        return true;
    }

    private static void FreeCurrentStream()
    {
        if (currentStream != 0)
        {
            Bass.ChannelStop(currentStream);
            Bass.StreamFree(currentStream);
            currentStream = 0;
        }

        streamEnded = false;
        endSyncProcedure = null;
    }

    public static readonly HashSet<string> supportedAudioExtensions = new()
    {
        ".mp3",
        ".ogg",
        ".wav",
        ".m4a",
        ".flac",
        ".ogg",
        ".wav"
    };

    public static readonly HashSet<string> supportedPlaylistExtensions = new()
    {
        ".m3u",
        ".m3u8",
    };

    public static void UpdateVolume()
    {
        if (currentStream == 0)
            return;

        Bass.ChannelSetAttribute(currentStream, ChannelAttribute.Volume, GetPlaybackGain());
    }

    private static float GetPlaybackGain()
    {
        float gain = UserVolume;

        if (AudioNormalisationEnabled && musicQueue.Count > 0)
        {
            SongInfo song = CurrentSong();

            if (song.LUFSCalculated)
            {
                float gainDb = targetLUFS - song.LUFS;

                gain *= Mathf.Pow(10f, gainDb / 20f);
            }
        }

        return gain;
    }

    public static float CalculateLUFS(string path)
    {
        int stream = Bass.CreateStream(
            path,
            Flags: BassFlags.Decode | BassFlags.Float
        );

        if (stream == 0)
        {
            Debug.LogError($"Failed to open stream: {Bass.LastError}");
            return float.NegativeInfinity;
        }

        try
        {
            var info = Bass.ChannelGetInfo(stream);

            int channels = info.Channels;
            int sampleRate = info.Frequency;

            // R128 integrated loudness meter.
            var meter = new IntegratedLoudnessMeter(
                channels,
                sampleRate
            );

            // Number of FLOAT samples, not bytes.
            float[] buffer = new float[8192];

            while (true)
            {
                int bytesRead = Bass.ChannelGetData(
                    stream,
                    buffer,
                    buffer.Length
                );

                if (bytesRead <= 0)
                    break;

                int samplesRead = bytesRead / sizeof(float);

                // R128Net expects interleaved float PCM.
                meter.AddFrames(
                    buffer,
                    0,
                    samplesRead
                );
            }

            double lufs = meter.IntegratedLoudness;

            meter.Dispose();

            return (float)lufs;
        }
        finally
        {
            Bass.StreamFree(stream);
        }
    }
}

//cross platform support
public interface IMediaControlProvider
{
    event Action PlayPausePressed;
    event Action NextPressed;
    event Action PreviousPressed;
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