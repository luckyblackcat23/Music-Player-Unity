using UnityEngine;

public class MediaInputManager : MonoBehaviour
{
    private IMediaPlatform mediaPlatform;

    void Start()
    {
        //change later
        mediaPlatform = new WindowsMediaPlatform();

        // Windows -> MusicPlayer
        mediaPlatform.PlayPauseRequested += MusicPlayer.TogglePause;
        mediaPlatform.NextRequested += HandlePlayNext;
        mediaPlatform.PreviousRequested += HandlePlayPrevious;
        mediaPlatform.SeekRequested += HandleSeek;

        // MusicPlayer -> Windows
        MusicPlayer.OnPlay.AddListener(HandlePlay);
        MusicPlayer.OnPause.AddListener(HandlePause);
        MusicPlayer.OnSongChange.AddListener(HandleSongChange);

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
        SongInfo song = MusicPlayer .CurrentSong();

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
        MusicPlayer .SetSongTime(seconds);

        // Immediately update Windows' timeline.
        mediaPlatform?.UpdatePosition(seconds);
    }

    private void HandlePlayNext()
    {
        MusicPlayer .PlayNext();
    }

    private void HandlePlayPrevious()
    {
        MusicPlayer .PlayPrevious();
    }

    void Update()
    {
        mediaPlatform?.Poll();
    }

    void OnDestroy()
    {
        MusicPlayer.OnPlay.RemoveListener(HandlePlay);
        MusicPlayer.OnPause.RemoveListener(HandlePause);
        MusicPlayer.OnSongChange.RemoveListener(HandleSongChange);

        mediaPlatform?.Shutdown();
    }
}