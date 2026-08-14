using System.Runtime.InteropServices;
using System.Diagnostics;
using UnityEngine;
using System.IO;
using System;

namespace SystemTheme
{
    public static class LinuxTheme
    {
        public static Color GetAccentColor()
        {
            string desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");

            if (!string.IsNullOrEmpty(desktop) &&
                desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase))
            {
                return GetKDEAccentColor();
            }

            if (!string.IsNullOrEmpty(desktop) &&
                desktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase))
            {
                return GetGNOMEAccentColor();
            }

            return Color.cyan;
        }


        private static Color GetKDEAccentColor()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "kdeglobals"
            );

            if (!File.Exists(path))
                return Color.cyan;

            string[] lines = File.ReadAllLines(path);

            bool inSelectionSection = false;

            foreach (string line in lines)
            {
                if (line.StartsWith("["))
                {
                    inSelectionSection =
                        line.Trim() == "[Colors:Selection]";

                    continue;
                }

                if (!inSelectionSection)
                    continue;

                if (!line.StartsWith("BackgroundNormal="))
                    continue;

                string value = line
                    .Substring("BackgroundNormal=".Length)
                    .Trim();

                string[] rgb = value.Split(',');

                if (rgb.Length != 3)
                    return Color.cyan;

                if (!byte.TryParse(rgb[0], out byte r) ||
                    !byte.TryParse(rgb[1], out byte g) ||
                    !byte.TryParse(rgb[2], out byte b))
                {
                    return Color.cyan;
                }

                return new Color32(r, g, b, 255);
            }

            return Color.cyan;
        }

        private static Color GetGNOMEAccentColor()
        {
            // Your gsettings implementation goes here.
            return Color.cyan;
        }
    }

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
                UnityEngine.Debug.LogWarning($"Failed to read Windows accent colour. Error: {result}");
                return Color.cyan;
            }

            byte b = (byte)((value >> 16) & 0xFF);
            byte g = (byte)((value >> 8) & 0xFF);
            byte r = (byte)(value & 0xFF);

            return new Color32(r, g, b, 255);
        }
    }
}