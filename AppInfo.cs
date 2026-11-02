using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace FastSnip;

static class AppInfo
{
    public const string Name = "FastSnip";
    public const string RepoUrl = "https://github.com/naterliu1115/FastSnip";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>Per-user data folder for settings and the error log.</summary>
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    static Icon? icon;

    public static Icon Icon
    {
        get
        {
            if (icon != null) return icon;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FastSnip.ico");
            icon = stream != null ? new Icon(stream) : SystemIcons.Application;
            return icon;
        }
    }
}
