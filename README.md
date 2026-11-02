# FastSnip

按下 PrtSc，0.1 秒內凍結畫面。框選、用像素級鉛筆標註，然後貼上。

Press PrtSc and the screen freezes in about 0.1 s. Select, annotate with a pixel-exact pencil, paste. ([English below](#english))

- **快**：常駐系統匣，截圖視窗事先建好，按下去不用等。Windows 剪取工具在同一台電腦上要 0.6～4.5 秒。
- **像素級鉛筆**：不做反鋸齒，1px 點一下就是一個像素，粗細 1～30px 都是實心方塊。適合畫像素圖、做遊戲的人標註素材。
- **小**：一個不到 100KB 的 exe，不用安裝執行環境（使用 Windows 內建的 .NET Framework 4.8）。
- **不連網**：不收集任何資料，截圖只會放進剪貼簿或你指定的資料夾。

## 安裝

1. 到 [Releases](../../releases) 下載 `FastSnip.exe`，放在你想放的資料夾（例如 `%LOCALAPPDATA%\FastSnip`）。
2. 執行。第一次開啟會詢問要不要用 PrtSc 截圖、要不要開機自動啟動。
3. 因為 exe 沒有程式碼簽章，Windows 可能會顯示「Windows 已保護您的電腦」，請按「其他資訊」→「仍要執行」。

需求：Windows 10 1903 以上或 Windows 11。

## 使用方式

| 操作 | 說明 |
|---|---|
| PrtSc | 凍結畫面，拖曳框選（快捷鍵可在設定裡更換） |
| 鉛筆 `P`／直線 `L`／圓形 `O` | 在選取範圍內標註；Shift 可讓直線鎖定 45°、圓形變正圓 |
| 滑桿、滾輪、`[` `]` | 調整目前工具的粗細（1～30px，每個工具各自記住） |
| `Ctrl+Z` | 復原上一筆 |
| `Enter`／`Ctrl+C`／✓ 複製 | 複製到剪貼簿 |
| `Ctrl+S`／存檔 | 存成 PNG（同時也會複製） |
| `Esc`／右鍵／✕ | 取消 |

右鍵系統匣圖示可以開啟設定、存檔資料夾或結束程式。程式已經在執行時再開一次 exe，會直接打開設定。

### 設定

- 截圖快捷鍵：PrtSc、Ctrl+PrtSc、Shift+PrtSc、Ctrl+Alt+A
- 框選完成後：顯示標註工具列，或直接複製
- 每次複製時也自動存成 PNG、存檔資料夾
- 語言：跟隨系統、繁體中文、English
- 開機時自動啟動

### 關於 PrtSc 鍵

Windows 11 預設把 PrtSc 交給剪取工具。要讓 FastSnip 使用 PrtSc，需要關閉「設定 → 協助工具 → 鍵盤 → 使用 Print Screen 鍵開啟螢幕擷取」。第一次執行和設定視窗都有按鈕可以幫你關閉，你也可以改用其他快捷鍵。

## 解除安裝

1. 右鍵系統匣圖示 → 設定，取消「開機時自動啟動」，再選「結束」。
2. 刪除 `FastSnip.exe` 和 `%APPDATA%\FastSnip` 資料夾。

## 從原始碼建置

需要 [.NET SDK](https://dotnet.microsoft.com/download) 6 以上。

```powershell
dotnet build -c Release            # 輸出 bin\Release\net48\FastSnip.exe
powershell -File tools\build-release.ps1   # 產生 dist\ 下的發布檔與 SHA-256
```

---

## English

- **Fast**: lives in the tray with the capture window pre-built, so the overlay appears in ~0.1 s.
- **Pixel-exact pencil**: no anti-aliasing; at 1 px a click sets exactly one pixel, up to 30 px solid squares. Made for pixel art and game dev.
- **Tiny**: a single exe under 100 KB with no runtime to install (uses the .NET Framework 4.8 built into Windows).
- **Offline**: collects nothing; captures only go to the clipboard or the folder you choose.

**Install**: download `FastSnip.exe` from [Releases](../../releases) and run it. The exe isn't code-signed, so SmartScreen may warn you: choose "More info" → "Run anyway". Requires Windows 10 1903+ or Windows 11.

**Use**: press PrtSc (configurable), drag to select, annotate with Pencil `P` / Line `L` / Ellipse `O` (Shift constrains), change size with the slider, mouse wheel or `[` `]`, `Ctrl+Z` to undo, `Enter`/`Ctrl+C` to copy, `Ctrl+S` to save a PNG, `Esc` to cancel. Right-click the tray icon for settings.

On Windows 11, PrtSc opens Snipping Tool by default. FastSnip can turn that off for you on first run or from Settings, or you can pick another hotkey.

**Uninstall**: turn off "Start with Windows" in Settings, exit from the tray, then delete `FastSnip.exe` and `%APPDATA%\FastSnip`.

## License

[MIT](LICENSE)
