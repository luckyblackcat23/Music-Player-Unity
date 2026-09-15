using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Audio;
using UnityEngine;
using ManagedBass;
using System.Linq;
using System.IO;
using R128Net;
using System;
using MyBox;
using Tools;
//using Kawazu;

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

    const float targetLUFS = -14f;

    private static SaveFloat userVolumeSave = new("userVolume", saveData);
    public float UserVolume
    {
        get => userVolumeSave;
        set
        {
            userVolumeSave.Set(Mathf.Clamp01(value), false);

            UpdateVolume();
        }
    }

    public static SaveFloat playbackTime = new SaveFloat("PlaybackTime", saveData);

    [ReadOnly]
    public float clipLength = 1;

    [ReadOnly]
    public bool paused = true;

    [ReadOnly]
    public bool shuffle = true;

    public static SaveBool AudioNormalisationEnabled = new SaveBool("AudioNormalisationEnabled", saveData, true);

    public List<SongInfo> musicQueue = new();

    public static SongInfo[] cachedSongs;

    public static SaveString currentSongPath = new SaveString("LastLoadedSongPath", saveData);

    [ReadOnly]
    public int currentSongIndex = 0;
    public SongInfo CurrentSong()
    {
        if (musicQueue == null || musicQueue?.Count == 0)
            return null;
        else
            return musicQueue[currentSongIndex];
    }

    [SerializeField] private AudioMixer audioMixer;

    public AudioSource audioSource;

    [Space(10)]

    public UnityEvent OnSongChange;
    public UnityEvent OnSongEnd;
    public UnityEvent OnPlay;
    public UnityEvent OnPause;
    public UnityEvent OnSongShuffle;

    public bool useExternalSongs;

    private void Awake()
    {
        OnSongChange.AddListener(() => currentSongPath.Set(CurrentSong().SongPath));

        if (string.IsNullOrEmpty(currentSongPath))
            playbackTime.Set(0, false);
        else
        {
            SongInfo lastSong;

            if (GetCachedSong(currentSongPath, out lastSong))
            {
                lastSong.GetSongInfo();

                PlayNow(lastSong, false);
            }
            else
                playbackTime.Set(0, false);
        }

        PlaylistDirectoryNode = FileNode.BuildTree(Globals.PlaylistsPath);

        audioSource = GetComponent<AudioSource>();
    }

    bool songEnding = true;

    // Update is called once per frame
    void Update()
    {
        if (audioSource.clip)
            playbackTime.Set(audioSource.time, false);

        if (!songEnding)
        {
            if (!audioSource.isPlaying && playbackTime == 0)
            {
                songEnding = true;

                OnSongEnd.Invoke();
                Debug.Log("playing next song");
                PlayNext();
            }
        }
    }

    public void Pause()
    {
        paused = true;
        audioSource.Pause();
    }

    /// <summary>
    /// Starts playback. Plays the audioclip.
    /// </summary>
    public void Play(bool startPlayback = true)
    {
        if (currentSongIndex >= musicQueue.Count)
        {
            Stop();
            return;
        }

        SongInfo currentSong = CurrentSong();

        if (!currentSong.MetaDataLoaded)
        {
            currentSong.GetSongInfo();
        }

        if (audioSource.clip == null)
        {
            StartCoroutine(GetClipFromFile(new FileInfo(currentSong.SongPath), clip =>
            {
                if (clip == null)
                {
                    playbackTime.Set(0, false);
                    return; // maybe log an error here?
                }

                if (clip != audioSource.clip)
                {
                    playbackTime.Set(0, false);
                }

                if (clip == null || clip != audioSource.clip)
                {
                    OnSongChange.Invoke();
                }

                audioSource.clip = clip;

                if (AudioNormalisationEnabled)
                {
                    if (currentSong.LUFS <= 0)
                    {
                        currentSong.LUFS = CalculateLUFS(currentSong.SongPath);
                        Debug.Log("LUFS = " + currentSong.LUFS);
                    }
                }

                UpdateVolume();

                clipLength = clip.length;
                audioSource.time = playbackTime;

                if (!startPlayback)
                    return;

                paused = false;

                audioSource.Play();

                songEnding = false;
            }));
        }
        else
        {
            if (!startPlayback)
                return;

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
    /// whole lot more but will EXPLAIN LATER
    /// </summary>
    public void PlayNext(bool startPlayback = true)
    {
        if (currentSongIndex + 1 < musicQueue.Count)
        {
            currentSongIndex++;

            Stop();

            Play(startPlayback);
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
    public void PlayPrevious(bool startPlayback = true)
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
    public void PlayNow(SongInfo song, bool startPlayback = true)
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
    public void PlayIndex(int index_, bool startPlayback = true)
    {
        Stop();
        currentSongIndex = index_;

        Play(startPlayback);
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

    public static FileNode PlaylistDirectoryNode;

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

    public void AddPlaylistNext(Playlist playlist)
    {
        foreach (SongInfo song in playlist.GetSongs())
        {
            musicQueue.Add(song);
        }
    }

    public void IncrementLoop(bool direction = true)
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

    public void ShuffleQueue()
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

    public static readonly HashSet<string> supportedAudioExtensions = new()
    {
        ".mp3",
        ".ogg",
        ".wav",
        ".m4a"
    };

    public static readonly HashSet<string> supportedPlaylistExtensions = new()
    {
        ".m3u",
        ".m3u8",
    };

    public void UpdateVolume()
    {
        float gainDb = 1f;

        if (AudioNormalisationEnabled)
        {
            if (musicQueue.Count > 0)
            {
                gainDb = targetLUFS / CurrentSong().LUFS;
            }
        }

        audioMixer.SetFloat("Volume", Mathf.Pow(10f, gainDb / 20f));

        audioSource.volume = UserVolume;
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