using UnityEngine;

/// <summary>
/// Small Unity-owned driver for the static MusicPlayer. Bass itself is initialized
/// and freed by BassManager; this component only forwards Unity lifecycle calls.
/// </summary>
internal sealed class MusicPlayerRunner : MonoBehaviour
{
    private void Awake()
    {
        MusicPlayer.InitializeRuntime();
    }

    private void Update()
    {
        MusicPlayer.Update();
    }

    private void OnDestroy()
    {
        MusicPlayer.ShutdownRuntime();
    }
}
