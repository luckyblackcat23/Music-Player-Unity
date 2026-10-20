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
        IntPtr hwnd = Globals.GetUnityWindow();

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

    public void Poll()
    {
        if (!initialized)
            return;

        int button = PollMediaButton();

        switch (button)
        {
            case 1: // Play
            case 2: // Pause
                PlayPauseRequested?.Invoke();
                break;

            case 3: // Next
                NextRequested?.Invoke();
                break;

            case 4: // Previous
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


/*
#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowsMediaPlatform : IMediaPlatform
{
    public const int KEYEVENTF_EXTENTEDKEY = 1;
    public const int KEYEVENTF_KEYUP = 0;
    public const int VK_MEDIA_NEXT_TRACK = 0xB0;
    public const int VK_MEDIA_PLAY_PAUSE = 0xB3;
    public const int VK_MEDIA_PREV_TRACK = 0xB1;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);

    private void Form1_Load(object sender, EventArgs e)
    {

    }

    private void button1_Click(object sender, EventArgs e)
    {
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
    }

    private void button2_Click(object sender, EventArgs e)
    {
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
    }

    private void button3_Click(object sender, EventArgs e)
    {
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
    }
}

#endif
*/