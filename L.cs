using System.Globalization;

namespace FastSnip;

/// <summary>UI strings in Traditional Chinese and English.</summary>
static class L
{
    static bool zh = CultureInfo.CurrentUICulture.Name.StartsWith("zh");

    /// <param name="language">"auto", "zh" or "en".</param>
    public static void Apply(string language) =>
        zh = language == "zh" || (language != "en" && CultureInfo.CurrentUICulture.Name.StartsWith("zh"));

    // Tray
    public static string TrayTooltip => zh ? "{0}：按 {1} 截圖" : "{0}: press {1} to capture";
    public static string MenuCapture => zh ? "截圖" : "Capture";
    public static string MenuOpenFolder => zh ? "開啟存檔資料夾" : "Open save folder";
    public static string MenuSettings => zh ? "設定…" : "Settings…";
    public static string MenuAbout => zh ? "關於" : "About";
    public static string MenuExit => zh ? "結束" : "Exit";
    public static string HotkeyFailed => zh
        ? "{0} 已被其他程式佔用，按這裡開啟設定換一個快捷鍵。"
        : "{0} is taken by another program. Click here to choose another hotkey.";
    public static string SavedTo => zh ? "已存檔：{0}" : "Saved: {0}";
    public static string SaveFailed => zh ? "存檔失敗：{0}" : "Save failed: {0}";
    public static string AboutText => zh
        ? "{0} v{1}\n\n按下快捷鍵立即截圖，可用鉛筆、直線、圓形標註。\n\n{2}\n\n要開啟專案頁面嗎？"
        : "{0} v{1}\n\nInstant screenshots with pencil, line and ellipse annotations.\n\n{2}\n\nOpen the project page?";
    public static string ErrorMessage => zh
        ? "發生未預期的錯誤，詳細內容已寫入：\n{0}"
        : "An unexpected error occurred. Details were written to:\n{0}";

    // Overlay toolbar
    public static string ToolPencil => zh ? "鉛筆" : "Pencil";
    public static string ToolLine => zh ? "直線" : "Line";
    public static string ToolEllipse => zh ? "圓形" : "Ellipse";
    public static string ToolUndo => zh ? "復原" : "Undo";
    public static string ToolSave => zh ? "存檔" : "Save";
    public static string ToolCopy => zh ? "✓ 複製" : "✓ Copy";

    // Settings
    public static string SettingsTitle => zh ? "設定" : "Settings";
    public static string Hotkey => zh ? "截圖快捷鍵" : "Capture hotkey";
    public static string AfterSelect => zh ? "框選完成後" : "After selecting";
    public static string AfterSelectEdit => zh ? "顯示標註工具列" : "Show annotation toolbar";
    public static string AfterSelectCopy => zh ? "直接複製到剪貼簿" : "Copy to clipboard immediately";
    public static string AutoSave => zh ? "每次複製時也自動存成 PNG" : "Also save a PNG on every copy";
    public static string SaveFolder => zh ? "存檔資料夾" : "Save folder";
    public static string Browse => zh ? "瀏覽…" : "Browse…";
    public static string Language => zh ? "語言" : "Language";
    public static string LanguageAuto => zh ? "跟隨系統" : "System default";
    public static string Autostart => zh ? "開機時自動啟動" : "Start with Windows";
    public static string WindowsPrtSc => zh ? "Windows 的 PrtSc 鍵目前會開啟剪取工具。" : "Windows currently opens Snipping Tool on PrtSc.";
    public static string WindowsPrtScFree => zh ? "Windows 的 PrtSc 鍵已交給其他程式。" : "Windows has released the PrtSc key.";
    public static string ReleasePrtSc => zh ? "讓出 PrtSc 鍵" : "Release PrtSc";
    public static string Ok => zh ? "確定" : "OK";
    public static string Cancel => zh ? "取消" : "Cancel";

    // First run
    public static string WelcomeTitle => zh ? "歡迎使用 {0}" : "Welcome to {0}";
    public static string WelcomeBody => zh
        ? "按下快捷鍵就能立刻凍結畫面、拖曳框選，再用鉛筆、直線、圓形標註後複製到剪貼簿。\n\n{0} 會在右下角系統匣常駐，右鍵圖示可以開啟設定。"
        : "Press the hotkey to freeze the screen, drag to select, annotate with pencil, line or ellipse, then copy to the clipboard.\n\n{0} lives in the system tray; right-click its icon for settings.";
    public static string WelcomeUsePrtSc => zh
        ? "用 PrtSc 截圖（會關閉 Windows「用 PrtSc 開啟剪取工具」的設定）"
        : "Use PrtSc to capture (turns off Windows' \"PrtSc opens Snipping Tool\" setting)";
    public static string WelcomeStart => zh ? "開始使用" : "Get started";
}
