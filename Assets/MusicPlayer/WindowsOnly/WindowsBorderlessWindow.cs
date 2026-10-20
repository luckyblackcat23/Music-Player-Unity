using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowsBorderlessWindow
{
    private const int GWL_STYLE = -16;

    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static void RemoveBorder()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd = Globals.GetUnityWindow();

        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError("Failed to get Unity window handle.");
            return;
        }

        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();

        // Remove the title bar while preserving window resizing.
        style &= ~WS_CAPTION;
        style |= WS_THICKFRAME;

        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));

        // Refresh the window frame.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
#endif
    }
}