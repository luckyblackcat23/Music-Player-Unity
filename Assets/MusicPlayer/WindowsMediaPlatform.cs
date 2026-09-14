#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowsMediaPlatform : IMediaPlatform
{
    private const string DLL = "MusicPlayerWindowsMedia";

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool InitializeMediaControls(IntPtr hwnd);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ShutdownMediaControls();

    [DllImport(DLL, EntryPoint = "SetPlaying", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetPlaying();

    [DllImport(DLL, EntryPoint = "SetPaused", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetPaused();

    [DllImport(DLL, EntryPoint = "SetStopped", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetStopped();

    [DllImport(DLL, EntryPoint = "SetMetadata", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetMetadata([MarshalAs(UnmanagedType.LPWStr)] string title, [MarshalAs(UnmanagedType.LPWStr)] string artist, [MarshalAs(UnmanagedType.LPWStr)] string album, double durationSeconds);

    [DllImport(DLL, EntryPoint = "SetArtwork", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetArtwork(byte[] data, int dataSize);

    [DllImport(DLL, EntryPoint = "UpdatePosition", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeUpdatePosition(double positionSeconds);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int PollMediaButton();

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern double PollSeekPosition();

    public event Action PlayRequested;
    public event Action PauseRequested;
    public event Action PlayPauseRequested;
    public event Action NextRequested;
    public event Action PreviousRequested;
    public event Action<float> SeekRequested;

    private bool initialized;

    public WindowsMediaPlatform()
    {
        Initialize();
    }

    private void Initialize()
    {
        IntPtr hwnd = GetUnityWindow();

        Debug.Log($"WindowsMediaPlatform HWND: {hwnd}");

        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError(
                "WindowsMediaPlatform: Could not find Unity window."
            );
            return;
        }

        initialized = InitializeMediaControls(hwnd);

        if (!initialized)
        {
            Debug.LogError(
                "WindowsMediaPlatform: Failed to initialize Windows media controls."
            );
            return;
        }

        Debug.Log("Windows media controls initialized.");
    }

    private static IntPtr GetUnityWindow()
    {
        return GetActiveWindow();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    public void Poll()
    {
        if (!initialized)
            return;

        int button = PollMediaButton();

        switch (button)
        {
            case 1:
                PlayRequested?.Invoke();
                break;

            case 2:
                PauseRequested?.Invoke();
                break;

            case 3:
                NextRequested?.Invoke();
                break;

            case 4:
                PreviousRequested?.Invoke();
                break;
        }

        double seekPosition = PollSeekPosition();

        if (seekPosition >= 0.0)
        {
            SeekRequested?.Invoke((float)seekPosition);
        }
    }

    public void SetPlaying()
    {
        if (initialized)
            NativeSetPlaying();
    }

    public void SetPaused()
    {
        if (initialized)
            NativeSetPaused();
    }

    public void SetStopped()
    {
        if (initialized)
            NativeSetStopped();
    }

    public void SetMetadata(
        string title,
        string artist,
        string album,
        float durationSeconds)
    {
        if (!initialized)
            return;

        NativeSetMetadata(
            title ?? "",
            artist ?? "",
            album ?? "",
            Math.Max(0.0, durationSeconds)
        );
    }

    public void SetArtwork(Texture2D artwork)
    {
        if (!initialized || artwork == null)
            return;

        try
        {
            byte[] png = artwork.EncodeToPNG();

            if (png == null || png.Length == 0)
                return;

            NativeSetArtwork(png, png.Length);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"WindowsMediaPlatform: Failed to set artwork: {e}"
            );
        }
    }

    public void UpdatePosition(float seconds)
    {
        if (!initialized)
            return;

        NativeUpdatePosition(
            Math.Max(0.0, seconds)
        );
    }

    public void Shutdown()
    {
        if (!initialized)
            return;

        NativeSetStopped();
        ShutdownMediaControls();

        initialized = false;
    }
}

#endif
