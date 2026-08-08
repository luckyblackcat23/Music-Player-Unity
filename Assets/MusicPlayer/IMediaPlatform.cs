using UnityEngine;
using System;

public interface IMediaPlatform
{
    // Called by your music player
    void SetPlaying();
    void SetPaused();

    void SetMetadata(string title, string artist, string album, float durationSeconds);

    void SetArtwork(Texture2D artwork);

    void UpdatePosition(float seconds);

    // Raised by the operating system
    event Action PlayRequested;
    event Action PauseRequested;
    event Action PlayPauseRequested;
    event Action NextRequested;
    event Action PreviousRequested;
}