# FF Logs Uploader（繁中服 / Dalamud API 13 版）

這是 [beer-psi/FFLogsUploaderPlugin](https://github.com/beer-psi/FFLogsUploaderPlugin) 的 fork，改成可以在**繁體中文版（TC 服）**的 Dalamud 上使用。

原作是一個非官方的遊戲內 FF Logs 上傳外掛，可以取代 Archon App 上傳 log、切割 log 檔，也可以做即時紀錄 (live logging)。

> 這是非官方工具，與 FF Logs 及原作者無關。它會以 Archon App 客戶端的身分與 FF Logs 伺服器溝通，是否符合 FF Logs 的使用條款請自行評估。如果遇到問題，請在這個 fork 回報，不要去找原作者。

## 為什麼需要這個 fork

- 繁中服的 Dalamud 目前是 **API 13**，原作的每個版本都是用 API 15 編譯的，Dalamud 會拒絕載入。
- 繁中服的 log 要上傳到 `cn.fflogs.com`，原作寫死的是 `www.fflogs.com`。
- Archon App 9.6.140 之後，FF Logs 的 parser 請求需要帶 `gameVersionId`。原作還沒有帶，所以會一直卡在「loading parser」。

## 相對於原作的修改

以原作 `v0.0.0.13`（commit `3bc2296`）為基礎，修改日期為 2026-10-06。

**降到 Dalamud API 13**
- `Dalamud.NET.Sdk` 從 15.0.0 改成 13.1.0（.NET 10 → .NET 9）
- 把 C# 14 的 `extension(Task)` 區塊改寫成一般的靜態方法
- `IAsyncDalamudPlugin` 改成 `IDalamudPlugin`，在建構子中載入，`Dispose` 時等待非同步清理完成
- `IDutyState.DutyWiped` 事件改用 API 13 的簽章 `(object? sender, ushort territory)`
- `ZoneInitEventArgs` 在 API 13 直接提供 sheet row，不需要 `.ValueNullable`
- API 13 沒有 `ImGuiColors.ErrorForeground`，改用 `DalamudRed`

**繁中服 / FF Logs 協定**
- 主機與報告連結從 `www.fflogs.com` 改成 `cn.fflogs.com`
- parser 網址加上 `&gameVersionId=ff-live-cn`，格式與 Archon App 9.6.140 相同
- 回報的客戶端版本從 9.5.0 改成 9.6.140
- parser 啟動失敗時把錯誤寫進 `/xllog`，原本只會停在 loading 畫面

**修正區域設定**
- 上傳時使用的區域是從設定檔讀取的，預設值是 1 (NA)，但 `cn.fflogs.com` 沒有這個選項。結果畫面上顯示繁中服，報告和解析實際用的卻是 NA，即時紀錄因此一場戰鬥都抓不到。
- 現在登入後（包含自動登入）會檢查設定檔裡的公會、區域、公開設定，如果伺服器沒有提供這個選項，就改成畫面上顯示的那一項。

**診斷**
- 自動登入、parser 下載（HTTP 狀態與耗時）、parser 啟動、即時紀錄報告使用的區域與檔案，都會寫進 `/xllog`
- parser 下載超過 60 秒會直接失敗，不會一直卡在載入畫面

**繁體中文介面**
- 視窗、分頁、按鈕、選項、說明、進度與錯誤訊息、聊天訊息、通知、指令說明都改成繁體中文
- `/xllog` 中的 Dalamud 日誌維持英文，方便搜尋，也方便跟原作比對

## 目前狀態

- [x] 可以在繁中服 Dalamud（API 13）載入，也可以登入
- [x] parser 可以正常載入
- [x] 即時紀錄可以抓到戰鬥並上傳
- [ ] 上傳 / 分割現有紀錄檔：尚未測試

如果卡在「正在載入 parser...」，請停用後重新啟用外掛。FF Logs 伺服器偶爾會沒有回應，超過 60 秒外掛就會顯示錯誤。

目前還沒有提供 custom repo 或編譯好的 release。repo 裡的 `repo.json` 是原作留下的，仍然指向原作的版本，**請不要把它加進 Dalamud**。

## 編譯

需要 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)，以及繁中服 Dalamud 的安裝資料夾（裡面要有 `Dalamud.dll`）。

```powershell
$env:DALAMUD_HOME = "$env:APPDATA\FFXIVSimpleLauncher\Dalamud\Injector\"
dotnet build FFLogsUploaderPlugin/FFLogsUploaderPlugin.csproj -c Release
```

編譯結果會在 `FFLogsUploaderPlugin/bin/Release/FFLogsUploaderPlugin/latest.zip`。

## 安裝（Dev Plugin）

1. 把 `latest.zip` 解壓縮到一個固定的資料夾。所有檔案都要保留，包含 `runtimes\win-x64\native\ClearScriptV8.win-x64.dll`。
2. 遊戲內輸入 `/xlsettings`，到「實驗性功能」→「開發外掛位置」，加入 `FFLogsUploaderPlugin.dll` 的完整路徑。
3. 在 `/xlplugins` 的開發工具分頁啟用外掛，然後用 `/pfflogs` 開啟主視窗。
4. 登入後確認區域 (Region) 是繁中服對應的選項。只有選擇「個人紀錄」時才會顯示區域選單。

## 授權

與原作相同，採用 [GNU AGPL-3.0](LICENSE.md)。原作著作權屬於 beerpsi。

---

*English: Fork of beer-psi/FFLogsUploaderPlugin rebuilt for the Traditional Chinese (TC) client's Dalamud API 13, targeting `cn.fflogs.com` with `gameVersionId=ff-live-cn`, with a Traditional Chinese UI. Unofficial; not affiliated with FF Logs or the original author. AGPL-3.0.*
