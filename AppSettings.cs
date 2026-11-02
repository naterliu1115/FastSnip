using System;
using System.Collections.Generic;
using System.IO;

namespace FastSnip;

/// <summary>User preferences, persisted as key=value lines in %APPDATA%\FastSnip\settings.ini.</summary>
sealed class AppSettings
{
    public string Hotkey = Hotkeys.Default.Name;
    public bool EditAfterSelect = true;
    public bool AutoSave;
    public string SaveFolder = DefaultSaveFolder;
    public string Language = "auto";
    public bool FirstRunDone;

    public static string DefaultSaveFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), AppInfo.Name);

    static string FilePath => Path.Combine(AppInfo.DataDir, "settings.ini");

    public static AppSettings Load()
    {
        var s = new AppSettings();
        if (!File.Exists(FilePath)) return s;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(FilePath))
        {
            int eq = line.IndexOf('=');
            if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }

        if (values.TryGetValue(nameof(Hotkey), out var hotkey) && Hotkeys.Find(hotkey) != null) s.Hotkey = hotkey;
        if (values.TryGetValue(nameof(EditAfterSelect), out var edit)) s.EditAfterSelect = edit != "0";
        if (values.TryGetValue(nameof(AutoSave), out var auto)) s.AutoSave = auto == "1";
        if (values.TryGetValue(nameof(SaveFolder), out var folder) && folder.Length > 0) s.SaveFolder = folder;
        if (values.TryGetValue(nameof(Language), out var lang) && lang is "auto" or "zh" or "en") s.Language = lang;
        if (values.TryGetValue(nameof(FirstRunDone), out var done)) s.FirstRunDone = done == "1";
        return s;
    }

    public void Save()
    {
        File.WriteAllLines(FilePath, new[]
        {
            $"{nameof(Hotkey)}={Hotkey}",
            $"{nameof(EditAfterSelect)}={(EditAfterSelect ? 1 : 0)}",
            $"{nameof(AutoSave)}={(AutoSave ? 1 : 0)}",
            $"{nameof(SaveFolder)}={SaveFolder}",
            $"{nameof(Language)}={Language}",
            $"{nameof(FirstRunDone)}={(FirstRunDone ? 1 : 0)}",
        });
    }
}
