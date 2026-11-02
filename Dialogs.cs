using System;
using System.Drawing;
using System.Windows.Forms;

namespace FastSnip;

/// <summary>Shared look for the small code-built dialogs; layout is all AutoSize so it follows the system font and DPI.</summary>
abstract class DialogBase : Form
{
    protected DialogBase(string title)
    {
        Text = title;
        Icon = AppInfo.Icon;
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);
        // A tray app has no window of its own in front, so make sure the dialog isn't left behind others.
        TopMost = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected static TableLayoutPanel Grid(int columns) => new()
    {
        ColumnCount = columns,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Fill,
    };

    protected static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange(controls);
        return row;
    }

    protected static Label Caption(string text, int maxWidth = 0) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 7, 12, 3),
    };

    protected Button Buttons(string okText, string? cancelText)
    {
        var ok = new Button { Text = okText, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 0) };
        AcceptButton = ok;
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Padding = new Padding(0, 12, 0, 0),
        };
        if (cancelText != null)
        {
            var cancel = new Button { Text = cancelText, DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(88, 0) };
            CancelButton = cancel;
            panel.Controls.Add(cancel);
        }
        panel.Controls.Add(ok);
        Controls.Add(panel);
        return ok;
    }
}

sealed class WelcomeForm : DialogBase
{
    readonly CheckBox usePrtSc = new() { Text = L.WelcomeUsePrtSc, AutoSize = true, Checked = true };
    readonly CheckBox autostart = new() { Text = L.Autostart, AutoSize = true, Checked = true };

    public bool UsePrtSc => usePrtSc.Checked;
    public bool StartWithWindows => autostart.Checked;

    public WelcomeForm() : base(string.Format(L.WelcomeTitle, AppInfo.Name))
    {
        var grid = Grid(1);
        grid.Controls.Add(Caption(string.Format(L.WelcomeBody, AppInfo.Name), 420));
        // Only offer the PrtSc switch when Windows actually holds the key.
        if (WindowsPrintScreen.IsBoundToSnipping) grid.Controls.Add(usePrtSc);
        grid.Controls.Add(autostart);
        Controls.Add(grid);
        Buttons(L.WelcomeStart, null);
    }
}

sealed class SettingsForm : DialogBase
{
    static readonly string[] LanguageValues = { "auto", "zh", "en" };

    readonly ComboBox hotkey = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly RadioButton afterEdit = new() { Text = L.AfterSelectEdit, AutoSize = true };
    readonly RadioButton afterCopy = new() { Text = L.AfterSelectCopy, AutoSize = true };
    readonly CheckBox autoSave = new() { Text = L.AutoSave, AutoSize = true };
    readonly TextBox folder = new() { Width = 300 };
    readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly CheckBox autostart = new() { Text = L.Autostart, AutoSize = true };

    public SettingsForm(AppSettings settings) : base($"{AppInfo.Name} {L.SettingsTitle}")
    {
        foreach (var h in Hotkeys.All) hotkey.Items.Add(h.Name);
        hotkey.SelectedItem = settings.Hotkey;
        afterEdit.Checked = settings.EditAfterSelect;
        afterCopy.Checked = !settings.EditAfterSelect;
        autoSave.Checked = settings.AutoSave;
        folder.Text = settings.SaveFolder;
        language.Items.AddRange(new object[] { L.LanguageAuto, "繁體中文", "English" });
        language.SelectedIndex = Math.Max(0, Array.IndexOf(LanguageValues, settings.Language));
        autostart.Checked = Autostart.IsEnabled;

        var browse = new Button { Text = L.Browse, AutoSize = true };
        browse.Click += (_, _) => BrowseFolder();

        var grid = Grid(2);
        grid.Controls.Add(Caption(L.Hotkey));
        grid.Controls.Add(hotkey);
        grid.Controls.Add(Caption(L.AfterSelect));
        var afterGroup = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty };
        afterGroup.Controls.Add(afterEdit);
        afterGroup.Controls.Add(afterCopy);
        grid.Controls.Add(afterGroup);
        grid.Controls.Add(Caption(L.SaveFolder));
        grid.Controls.Add(Row(folder, browse));
        grid.Controls.Add(new Label());
        grid.Controls.Add(autoSave);
        grid.Controls.Add(Caption(L.Language));
        grid.Controls.Add(language);
        grid.Controls.Add(new Label());
        grid.Controls.Add(autostart);
        grid.Controls.Add(new Label());
        grid.Controls.Add(PrtScRow());
        Controls.Add(grid);

        Buttons(L.Ok, L.Cancel);
    }

    Control PrtScRow()
    {
        var status = Caption(WindowsPrintScreen.IsBoundToSnipping ? L.WindowsPrtSc : L.WindowsPrtScFree);
        var release = new Button { Text = L.ReleasePrtSc, AutoSize = true, Visible = WindowsPrintScreen.IsBoundToSnipping };
        release.Click += (_, _) =>
        {
            WindowsPrintScreen.Release();
            status.Text = L.WindowsPrtScFree;
            release.Visible = false;
        };
        return Row(status, release);
    }

    void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = folder.Text, ShowNewFolderButton = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
    }

    /// <summary>Copies the dialog's values into <paramref name="settings"/> and applies autostart.</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.Hotkey = (string)hotkey.SelectedItem;
        settings.EditAfterSelect = afterEdit.Checked;
        settings.AutoSave = autoSave.Checked;
        settings.SaveFolder = folder.Text.Trim().Length > 0 ? folder.Text.Trim() : AppSettings.DefaultSaveFolder;
        settings.Language = LanguageValues[language.SelectedIndex];
        Autostart.Set(autostart.Checked);
    }
}
