using UnityEngine;

/// <summary>
/// Example integration. Attach to a GameObject in your Windows player.
/// Replace Globals.GetUnityWindow() with your project's existing HWND helper.
/// </summary>
public sealed class WindowsDragArea : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private int dragTop = 0;
    private int dragLeft = 40;
    private int dragRight = 40;
    private int dragBottom = 40;

    private void Start()
    {
        var hwnd = Globals.GetUnityWindow();

        Debug.Log($"Unity HWND: 0x{hwnd.ToInt64():X}");

        if (hwnd == System.IntPtr.Zero)
        {
            Debug.LogError("Could not obtain the Unity window handle.");
            return;
        }

        bool success = WindowsWindow.Initialize(
            hwnd, dragTop, dragLeft, dragRight, dragBottom);

        Debug.Log($"Drag area initialization: {success}");
    }
#endif
}
