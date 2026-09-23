using UnityEngine;

public class MediaInputManager : MonoBehaviour
{
    private IMediaPlatform mediaPlatform;
    public MusicPlayer musicPlayer;

    void Start()
    {
        mediaPlatform = new WindowsMediaPlatform();

        // Windows -> MusicPlayer
        mediaPlatform.PlayPauseRequested += musicPlayer.TogglePause;
        mediaPlatform.NextRequested += HandlePlayNext;
        mediaPlatform.PreviousRequested += HandlePlayPrevious;
        mediaPlatform.SeekRequested += HandleSeek;

        // MusicPlayer -> Windows
        musicPlayer.OnPlay.AddListener(HandlePlay);
        musicPlayer.OnPause.AddListener(HandlePause);
        musicPlayer.OnSongChange.AddListener(HandleSongChange);

        // If a song is already loaded when this starts,
        // immediately give Windows its metadata.
        HandleSongChange();
    }

    private void HandlePlay()
    {
        mediaPlatform?.SetPlaying();
    }

    private void HandlePause()
    {
        mediaPlatform?.SetPaused();
    }

    private void HandleSongChange()
    {
        if (musicPlayer == null)
            return;

        SongInfo song = musicPlayer.CurrentSong();

        if (song == null)
            return;

        mediaPlatform?.SetMetadata(song.Title, song.Artist, song.Album, (float)song.Duration);

        if (song.AlbumCover != null)
        {
            mediaPlatform?.SetArtwork(song.AlbumCover);
        }
    }

    private void HandleSeek(float seconds)
    {
        // Replace this with your actual MusicPlayer seek method.
        musicPlayer.SetSongTime(seconds);

        // Immediately update Windows' timeline.
        mediaPlatform?.UpdatePosition(seconds);
    }

    private void HandlePlayNext()
    {
        musicPlayer.PlayNext();
    }

    private void HandlePlayPrevious()
    {
        musicPlayer.PlayPrevious();
    }

    void Update()
    {
        mediaPlatform?.Poll();
    }

    void OnDestroy()
    {
        if (musicPlayer != null)
        {
            musicPlayer.OnPlay.RemoveListener(HandlePlay);
            musicPlayer.OnPause.RemoveListener(HandlePause);
            musicPlayer.OnSongChange.RemoveListener(HandleSongChange);
        }

        mediaPlatform?.Shutdown();
    }
}