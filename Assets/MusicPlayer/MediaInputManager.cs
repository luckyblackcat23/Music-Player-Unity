using UnityEngine;

public class MediaInputManager : MonoBehaviour
{
    private WindowsMediaPlatform mediaPlatform;

    public MusicPlayer musicPlayer;

    void Start()
    {
        mediaPlatform =
            new WindowsMediaPlatform();

        mediaPlatform.PlayRequested +=
            musicPlayer.Play;

        mediaPlatform.PauseRequested +=
            musicPlayer.Pause;

        mediaPlatform.NextRequested +=
            musicPlayer.PlayNext;

        mediaPlatform.PreviousRequested +=
            musicPlayer.PlayPrevious;

        mediaPlatform.SeekRequested +=
            HandleSeek;
    }

    private void HandleSeek(float seconds)
    {
        // Connect this to your MusicPlayer's seeking method.
        //
        // For example, if your MusicPlayer has:
        //
        // musicPlayer.Seek(seconds);
        //
        // then:
        //
        // musicPlayer.Seek(seconds);
    }

    void Update()
    {
        mediaPlatform?.Poll();
    }

    void OnDestroy()
    {
        mediaPlatform?.Shutdown();
    }
}