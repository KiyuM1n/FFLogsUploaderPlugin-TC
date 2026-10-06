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
            ImGui.Text($"Logged in as {user.User.UserName}");  
            
            ImGui.SameLine();
            
            // Disable logging out if logging is in operation or if the parser has not finished loading
            // (either successfully or failed)
            using (ImRaii.Disabled(AnyOperationInProgress || !plugin.FFLogs.ParsersReady))
            {
                if (ImGui.Button("Log out"))
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
            ImGui.Text("Currently not logged in.");
        }
        
        
        using (ImRaii.Disabled(AnyOperationInProgress))
        {
            if (ImGui.Checkbox("Start live logging when entering duty", ref startLiveLoggingWhenDutyStarts))
            {
                plugin.Configuration.StartLiveLoggingWhenDutyStarts = startLiveLoggingWhenDutyStarts;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Covers dungeons, trials, raids, alliance raids, chaotic alliance raids, ultimate raids.\nUnrestricted parties do not automatically start live logging, but duty support currently will.\nOptions are taken from the Live Log tab, except \"Include entire file in report\"\nwill always be disabled, and description will always be empty.\nMay have issues with unsupported dungeons.");
            }

            if (ImGui.Checkbox("Stop live logging 5 seconds after leaving duty", ref stopLiveLoggingWhenDutyEnds))
            {
                plugin.Configuration.StopLiveLoggingWhenDutyEnds = stopLiveLoggingWhenDutyEnds;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("The delay is necessary to allow ACT to finish writing logs, and for the uploader to finish parsing them.");
            }

            if (ImGui.Checkbox("Automatically call wipes when live logging", ref automaticallyCallDutyWipe))
            {
                plugin.Configuration.AutomaticallyCallDutyWipe = automaticallyCallDutyWipe;
                plugin.Configuration.Save();
            }

            if (ImGui.Checkbox("EngageTimer stopwatch resets on phase change", ref engageTimerPerPhase))
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
                ImGui.SetTooltip("- Requires EngageTimer plugin installed.\n- Scans for logs from the live logging folder and parses them for phase changes.\n- Only applies to things FFLogs consider to have phases (which are basically only ultimates).");
            }
        }
    }
}
