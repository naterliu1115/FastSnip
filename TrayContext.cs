using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace FastSnip;

sealed class TrayContext : ApplicationContext
{
    readonly AppSettings settings = AppSettings.Load();
    readonly OverlayForm overlay;
    readonly HotkeyWindow hotkey = new();
    readonly NotifyIcon tray;
    Action? balloonClick;

    public TrayContext()
    {
        L.Apply(settings.Language);
        if (!settings.FirstRunDone) RunWelcome();

        overlay = new OverlayForm(settings);
        _ = overlay.Handle; // create the window now, not on first capture
        overlay.Warmup();
        overlay.Saved += path => ShowBalloon(string.Format(L.SavedTo, path), ToolTipIcon.Info,
            () => Process.Start("explorer.exe", $"/select,\"{path}\""));
        overlay.SaveFailed += message => ShowBalloon(string.Format(L.SaveFailed, message), ToolTipIcon.Error, OpenSettings);

        tray = new NotifyIcon { Icon = AppInfo.Icon, Visible = true };
        tray.DoubleClick += (_, _) => overlay.StartCapture();
        tray.BalloonTipClicked += (_, _) => balloonClick?.Invoke();

        hotkey.Pressed += overlay.StartCapture;
        hotkey.ShowSettingsRequested += OpenSettings;
        ApplySettings();
    }

    void RunWelcome()
    {
        using var welcome = new WelcomeForm();
        welcome.ShowDialog();
        if (WindowsPrintScreen.IsBoundToSnipping)
        {
            if (welcome.UsePrtSc) WindowsPrintScreen.Release();
            else settings.Hotkey = Hotkeys.NoPrtSc.Name;
        }
        Autostart.Set(welcome.StartWithWindows);
        settings.FirstRunDone = true;
        settings.Save();
    }

    /// <summary>(Re)builds everything that depends on settings: language, menu, tooltip, hotkey.</summary>
    void ApplySettings()
    {
        L.Apply(settings.Language);
        var key = Hotkeys.Find(settings.Hotkey) ?? Hotkeys.Default;

        var menu = new ContextMenuStrip();
        menu.Items.Add($"{L.MenuCapture} ({key.Name})", null, (_, _) => overlay.StartCapture());
        menu.Items.Add(L.MenuOpenFolder, null, (_, _) => OpenSaveFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.MenuSettings, null, (_, _) => OpenSettings());
        menu.Items.Add(L.MenuAbout, null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.MenuExit, null, (_, _) => ExitThread());
        var old = tray.ContextMenuStrip;
        tray.ContextMenuStrip = menu;
        old?.Dispose();
        tray.Text = string.Format(L.TrayTooltip, AppInfo.Name, key.Name);

        if (!hotkey.Register(key))
            ShowBalloon(string.Format(L.HotkeyFailed, key.Name), ToolTipIcon.Warning, OpenSettings);
    }

    SettingsForm? settingsForm;

    void OpenSettings()
    {
        if (settingsForm != null)
        {
            settingsForm.Activate();
            return;
        }
        using (settingsForm = new SettingsForm(settings))
        {
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settingsForm.ApplyTo(settings);
                settings.Save();
                ApplySettings();
            }
        }
        settingsForm = null;
    }

    void OpenSaveFolder()
    {
        Directory.CreateDirectory(settings.SaveFolder);
        Process.Start("explorer.exe", $"\"{settings.SaveFolder}\"");
    }

    void ShowAbout()
    {
        var text = string.Format(L.AboutText, AppInfo.Name, AppInfo.Version, AppInfo.RepoUrl);
        if (MessageBox.Show(text, AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            Process.Start(AppInfo.RepoUrl);
    }

    void ShowBalloon(string text, ToolTipIcon icon, Action? onClick)
    {
        balloonClick = onClick;
        tray.ShowBalloonTip(4000, AppInfo.Name, text, icon);
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        tray.Dispose();
        hotkey.Dispose();
        overlay.Dispose();
        base.ExitThreadCore();
    }
}
