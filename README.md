# FF Logs Uploader（繁中服 / Dalamud API 13 版）

這是 [beer-psi/FFLogsUploaderPlugin](https://github.com/beer-psi/FFLogsUploaderPlugin) 的 fork，改成可以在 **繁體中文版（TC 服）** 的 Dalamud 上使用。

原作是一個非官方的遊戲內 FF Logs 上傳外掛，可以取代 Archon App 上傳 log、切割 log 檔，也可以做即時紀錄 (live logging)。

> 這是非官方工具，與 FF Logs 及原作者無關。它會以 Archon App 客戶端的身分與 FF Logs 伺服器溝通，是否符合 FF Logs 的使用條款請自行評估。如果遇到問題，請在這個 fork 回報，不要去找原作者。

相對於原作的修改內容，請見 [commit 紀錄](https://github.com/KiyuM1n/FFLogsUploaderPlugin-TC/commits/master)。

## 安裝

1. 遊戲內輸入 `/xlsettings`，到「實驗性功能」→「自訂外掛倉庫」，加入以下網址並儲存：

   ```
   https://raw.githubusercontent.com/KiyuM1n/FFLogsUploaderPlugin-TC/master/repo.json
   ```

2. 打開 `/xlplugins`，搜尋 **FF Logs Uploader** 並安裝，然後用 `/pfflogs` 開啟主視窗。
3. 登入後確認區域 (Region) 是繁中服對應的選項。只有選擇「個人紀錄」時才會顯示區域選單。

如果之前加過原作的倉庫網址（`beer-psi/FFLogsUploaderPlugin`），請先移除，兩者的外掛名稱相同會互相衝突。

如果卡在「正在載入 parser...」，請停用後重新啟用外掛。FF Logs 伺服器偶爾會沒有回應，超過 60 秒外掛就會顯示錯誤。

## 編譯

需要 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)，以及繁中服 Dalamud 的安裝資料夾（裡面要有 `Dalamud.dll`）。

```powershell
$env:DALAMUD_HOME = "$env:APPDATA\FFXIVSimpleLauncher\Dalamud\Injector\"
dotnet build FFLogsUploaderPlugin/FFLogsUploaderPlugin.csproj -c Release
```

編譯結果會在 `FFLogsUploaderPlugin/bin/Release/FFLogsUploaderPlugin/latest.zip`。

自己編譯的版本可以用 Dev Plugin 方式載入：把 `latest.zip` 解壓縮到固定的資料夾，在 `/xlsettings` →「實驗性功能」→「開發外掛位置」加入 `FFLogsUploaderPlugin.dll` 的完整路徑。

## 授權

與原作相同，採用 [GNU AGPL-3.0](LICENSE.md)。原作著作權屬於 beerpsi。

---

*English: Fork of beer-psi/FFLogsUploaderPlugin rebuilt for the Traditional Chinese (TC) client's Dalamud API 13, targeting `cn.fflogs.com` with `gameVersionId=ff-live-cn`, with a Traditional Chinese UI. Unofficial; not affiliated with FF Logs or the original author. AGPL-3.0. Custom repo: `https://raw.githubusercontent.com/KiyuM1n/FFLogsUploaderPlugin-TC/master/repo.json`*
