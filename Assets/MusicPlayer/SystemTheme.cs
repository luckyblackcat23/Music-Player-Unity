#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowsTheme
{
    private static readonly UIntPtr HKEY_CURRENT_USER =
        new UIntPtr(0x80000001);

    private const uint RRF_RT_REG_DWORD = 0x00000018;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegGetValue(
        UIntPtr hkey,
        string lpSubKey,
        string lpValue,
        uint dwFlags,
        out uint pdwType,
        out uint pvData,
        ref uint pcbData
    );

    public static Color GetAccentColor()
    {
        uint dataSize = 4;

        int result = RegGetValue(
            HKEY_CURRENT_USER,
            @"Software\Microsoft\Windows\DWM",
            "AccentColor",
            RRF_RT_REG_DWORD,
            out _,
            out uint value,
            ref dataSize
        );

        if (result != 0)
        {
            Debug.LogWarning($"Failed to read Windows accent colour. Error: {result}");
            return Color.cyan;
        }

        byte b = (byte)((value >> 16) & 0xFF);
        byte g = (byte)((value >> 8) & 0xFF);
        byte r = (byte)(value & 0xFF);

        return new Color32(r, g, b, 255);
    }
}
#endif