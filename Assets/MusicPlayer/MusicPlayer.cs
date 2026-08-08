using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Events;
using UnityEngine;
using ManagedBass;
using System.Linq;
using System.IO;
using System;
using Tools;
using MyBox;
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

        var k = Keyboard.current;

        if (k == null)
            return;

        if (k.mediaPlayPause.wasPressedThisFrame)
            Debug.Log("Play/Pause");

        if (k.mediaForward.wasPressedThisFrame)
            Debug.Log("Next");

        if (k.mediaRewind.wasPressedThisFrame)
            Debug.Log("Previous");
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

    public void AddPlaylistNext(Playlist playlist)
    {
        foreach (SongInfo song in playlist.GetSongs())
        {
            musicQueue.Add(song);
        }
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