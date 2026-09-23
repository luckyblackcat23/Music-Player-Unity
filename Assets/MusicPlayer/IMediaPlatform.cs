using System;
using UnityEngine;

public interface IMediaPlatform
{
    void SetPlaying();
    void SetPaused();

    void SetMetadata(
        string title,
        string artist,
        string album,
        float durationSeconds);

    void SetArtwork(Texture2D artwork);

    void UpdatePosition(float seconds);
    void Shutdown();
    void Poll();

    event Action PlayPauseRequested;
    event Action NextRequested;
    event Action PreviousRequested;
    event Action<float> SeekRequested;
}