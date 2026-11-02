using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FastSnip;

record Hotkey(string Name, uint Modifiers, uint Key);

static class Hotkeys
{
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4;
    const uint VK_SNAPSHOT = 0x2C;

    public static readonly Hotkey[] All =
    {
        new("PrtSc", 0, VK_SNAPSHOT),
        new("Ctrl+PrtSc", MOD_CONTROL, VK_SNAPSHOT),
        new("Shift+PrtSc", MOD_SHIFT, VK_SNAPSHOT),
        new("Ctrl+Alt+A", MOD_CONTROL | MOD_ALT, (uint)Keys.A),
    };

    public static Hotkey Default => All[0];

    /// <summary>Fallback when the user keeps Windows' own PrtSc binding.</summary>
    public static Hotkey NoPrtSc => All[1];

    public static Hotkey? Find(string name) => Array.Find(All, h => h.Name == name);
}

/// <summary>"Start with Windows" via the per-user Run key.</summary>
static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppInfo.Name) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(AppInfo.Name, $"\"{Application.ExecutablePath}\"");
        else key.DeleteValue(AppInfo.Name, false);
    }
}

/// <summary>Windows 11's "Use the Print screen key to open screen capture" setting.</summary>
static class WindowsPrintScreen
{
    const string KeyPath = @"Control Panel\Keyboard";
    const string ValueName = "PrintScreenKeyForSnippingEnabled";

    /// <summary>True when Windows itself grabs PrtSc for Snipping Tool.</summary>
    public static bool IsBoundToSnipping
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(ValueName) is int value) return value != 0;
            // Unset means the OS default: on for Windows 11 (build 22000+), off for Windows 10.
            return Environment.OSVersion.Version.Build >= 22000;
        }
    }

    public static void Release()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, 0, RegistryValueKind.DWord);
    }
}
