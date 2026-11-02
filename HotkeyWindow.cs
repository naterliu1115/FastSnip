using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FastSnip;

/// <summary>
/// Hidden window that receives WM_HOTKEY for a single global hotkey, plus the
/// "show settings" broadcast a second launch of the exe sends.
/// </summary>
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int HotkeyId = 1;
    const uint MOD_NOREPEAT = 0x4000;
    static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    static readonly int ShowSettingsMessage = RegisterWindowMessage("FastSnip.ShowSettings");

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    bool registered;

    public event Action? Pressed;
    public event Action? ShowSettingsRequested;

    public static void RequestShowSettings() => PostMessage(HWND_BROADCAST, ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);

    public HotkeyWindow() => CreateHandle(new CreateParams());

    /// <summary>Replaces any previously registered hotkey.</summary>
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        registered = RegisterHotKey(Handle, HotkeyId, hotkey.Modifiers | MOD_NOREPEAT, hotkey.Key);
        return registered;
    }

    void Unregister()
    {
        if (registered) UnregisterHotKey(Handle, HotkeyId);
        registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && (int)m.WParam == HotkeyId)
            Pressed?.Invoke();
        else if (m.Msg == ShowSettingsMessage)
            ShowSettingsRequested?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}
