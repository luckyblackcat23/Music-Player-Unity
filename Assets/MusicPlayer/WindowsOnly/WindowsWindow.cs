using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows-only wrapper for the CustomWindowsTitleBar drag-area DLL.
/// Window buttons (close/minimize/maximize) remain implemented in Unity.
/// </summary>
public static class WindowsWindow
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const string DllName = "CustomWindowsTitleBar";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeDragArea(
        IntPtr hwnd, int top, int left, int right, int bottom);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDragArea(
        int top, int left, int right, int bottom);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ShutdownDragArea();
#endif

    /// <summary>
    /// Installs the drag-area handling on the supplied Unity window handle.
    /// Coordinates are in client-area pixels: top/bottom are Y coordinates;
    /// left/right are margins from the corresponding horizontal edges.
    /// </summary>
    public static bool Initialize(IntPtr hwnd, int top, int left, int right, int bottom)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool success = InitializeDragArea(hwnd, top, left, right, bottom);
        if (!success)
            Debug.LogError("CustomWindowsTitleBar: failed to initialize the drag area.");
        return success;
#else
        return false;
#endif
    }

    /// <summary>Updates the rectangle after initialization.</summary>
    public static bool ConfigureDragArea(int top, int left, int right, int bottom)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool success = SetDragArea(top, left, right, bottom);
        if (!success)
            Debug.LogError("CustomWindowsTitleBar: failed to update the drag area.");
        return success;
#else
        return false;
#endif
    }

    /// <summary>Restores the original window procedure.</summary>
    public static void Shutdown()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        ShutdownDragArea();
#endif
    }
}
