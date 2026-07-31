using ManagedBass;
using UnityEngine;

public static class BassManager
{
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
            return;

        initialized = Bass.Init();

        if (!initialized)
        {
            Debug.LogError($"BASS failed: {Bass.LastError}");
            return;
        }

        Application.quitting += () => Bass.Free();
    }
}