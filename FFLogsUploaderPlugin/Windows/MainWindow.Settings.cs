using System;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace FFLogsUploaderPlugin.Windows;

public partial class MainWindow
{
    private bool automaticallyCallDutyWipe;
    private bool startLiveLoggingWhenDutyStarts;
    private bool stopLiveLoggingWhenDutyEnds;
    private bool engageTimerPerPhase;
    
    public void SetOptionsFromConfiguration()
    {
        email = plugin.Configuration.FfLogsEmail;
        password = plugin.Configuration.FfLogsPassword;
        automaticLogin = plugin.Configuration.FfLogsAutomaticLogin;
        logFilePath = plugin.Configuration.LogFilePath;
        logFolder = plugin.Configuration.LiveLogFolder;
        includeEntireFileInReport = plugin.Configuration.IncludeEntireFileInReport;
        automaticallyCallDutyWipe = plugin.Configuration.AutomaticallyCallDutyWipe;
        startLiveLoggingWhenDutyStarts = plugin.Configuration.StartLiveLoggingWhenDutyStarts;
        stopLiveLoggingWhenDutyEnds = plugin.Configuration.StopLiveLoggingWhenDutyEnds;
        splitLogGroupSameContent = plugin.Configuration.SplitLogGroupSameContent;
        engageTimerPerPhase = plugin.Configuration.EngageTimerPerPhase;

        if (plugin.FFLogs.User != null)
        {
            selectedGuildIndex =
                plugin.FFLogs.User!.GuildSelectItems.FindIndex(item => item.Value ==
                                                                       plugin.Configuration.SelectedGuildValue);
            selectedRegionIndex =
                plugin.FFLogs.User.RegionOrServerSelectItems.FindIndex(item => item.Value ==
                                                                               plugin.Configuration.SelectedRegionValue);
            selectedVisibilityIndex =
                plugin.FFLogs.User.ReportVisibilitySelectItems.FindIndex(item => item.Value ==
                                                                             plugin.Configuration.SelectedVisibilityValue);

            if (selectedGuildIndex == -1) selectedGuildIndex = 0;
            if (selectedRegionIndex == -1) selectedRegionIndex = 0;
            if (selectedVisibilityIndex == -1) selectedVisibilityIndex = 0;

            // Uploads read these values from the configuration, not from the combos, so a saved value that the
            // server no longer offers (e.g. the default NA region on cn.fflogs.com) must be replaced by what the
            // combo shows.
            var user = plugin.FFLogs.User;
            var changed = false;

            if (user.GuildSelectItems.Count > 0
                && plugin.Configuration.SelectedGuildValue != user.GuildSelectItems[selectedGuildIndex].Value)
            {
                plugin.Configuration.SelectedGuildValue = user.GuildSelectItems[selectedGuildIndex].Value;
                changed = true;
            }

            if (user.RegionOrServerSelectItems.Count > 0
                && plugin.Configuration.SelectedRegionValue != user.RegionOrServerSelectItems[selectedRegionIndex].Value)
            {
                plugin.Configuration.SelectedRegionValue = user.RegionOrServerSelectItems[selectedRegionIndex].Value;
                changed = true;
            }

            if (user.ReportVisibilitySelectItems.Count > 0
                && plugin.Configuration.SelectedVisibilityValue != user.ReportVisibilitySelectItems[selectedVisibilityIndex].Value)
            {
                plugin.Configuration.SelectedVisibilityValue = user.ReportVisibilitySelectItems[selectedVisibilityIndex].Value;
                changed = true;
            }

            if (changed)
                plugin.Configuration.Save();
        }
    }
    
    private void DrawSettingsTab()
    {
        ImGui.Spacing();
        if (plugin.FFLogs.User is { } user)
        {
            ImGui.Text($"已登入：{user.User.UserName}");  
            
            ImGui.SameLine();
            
            // Disable logging out if logging is in operation or if the parser has not finished loading
            // (either successfully or failed)
            using (ImRaii.Disabled(AnyOperationInProgress || !plugin.FFLogs.ParsersReady))
            {
                if (ImGui.Button("登出"))
                {
                    email = string.Empty;
                    password = string.Empty;
                    automaticLogin = false;

                    Task.Run(() => plugin.FFLogs.LogoutAsync());
                }
            }
        }
        else
        {
            ImGui.Text("目前未登入。");
        }
        
        
        using (ImRaii.Disabled(AnyOperationInProgress))
        {
            if (ImGui.Checkbox("進入副本時自動開始即時紀錄", ref startLiveLoggingWhenDutyStarts))
            {
                plugin.Configuration.StartLiveLoggingWhenDutyStarts = startLiveLoggingWhenDutyStarts;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("適用於迷宮挑戰、討伐殲滅戰、大型任務、24 人大型任務、混沌聯盟戰和絕境戰。\n解除限制的隊伍不會自動開始，但目前剿滅支援 (Duty Support) 會。\n選項沿用「即時紀錄」分頁的設定，但「將整個紀錄檔加入報告」一律關閉，\n報告說明一律留空。\n不支援的迷宮挑戰可能會有問題。");
            }

            if (ImGui.Checkbox("離開副本 5 秒後自動停止即時紀錄", ref stopLiveLoggingWhenDutyEnds))
            {
                plugin.Configuration.StopLiveLoggingWhenDutyEnds = stopLiveLoggingWhenDutyEnds;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("這段延遲是為了讓 ACT 寫完紀錄，並讓上傳工具處理完畢。");
            }

            if (ImGui.Checkbox("即時紀錄時自動判定滅團", ref automaticallyCallDutyWipe))
            {
                plugin.Configuration.AutomaticallyCallDutyWipe = automaticallyCallDutyWipe;
                plugin.Configuration.Save();
            }

            if (ImGui.Checkbox("EngageTimer 計時器在換階段時重置", ref engageTimerPerPhase))
            {
                plugin.Configuration.EngageTimerPerPhase = engageTimerPerPhase;
                plugin.Configuration.Save();

                if (engageTimerPerPhase && !plugin.FFLogs.IsMonitoringActive)
                    plugin.FFLogs.StartMetersLogCollectionAsync();
                else if (!engageTimerPerPhase && plugin.FFLogs.IsMonitoringActive)
                    plugin.FFLogs.StopMetersLogCollection();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("- 需要安裝 EngageTimer 外掛。\n- 會讀取即時紀錄資料夾中的紀錄檔來判斷換階段。\n- 只適用於 FF Logs 有分階段的戰鬥（基本上只有絕境戰）。");
            }
        }
    }
}
