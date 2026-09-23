#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowsTitleBar
{
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    public static void SetColour(Color colour)
    {
        IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;

        int captionColour = ToBgr(colour);

        DwmSetWindowAttribute(
            hwnd,
            DWMWA_CAPTION_COLOR,
            ref captionColour,
            sizeof(int));

        // Choose black or white title-bar text depending on brightness.
        Color.RGBToHSV(colour, out _, out _, out float value);

        int textColour = value > 0.5f
            ? ToBgr(Color.black)
            : ToBgr(Color.white);

        DwmSetWindowAttribute(
            hwnd,
            DWMWA_TEXT_COLOR,
            ref textColour,
            sizeof(int));
    }

    private static int ToBgr(Color colour)
    {
        int r = Mathf.RoundToInt(colour.r * 255f);
        int g = Mathf.RoundToInt(colour.g * 255f);
        int b = Mathf.RoundToInt(colour.b * 255f);

        return r | (g << 8) | (b << 16);
    }
}

#endif