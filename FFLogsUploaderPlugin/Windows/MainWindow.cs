using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFLogsUploaderPlugin.Integration;

namespace FFLogsUploaderPlugin.Windows;

public partial class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly FileDialogManager fileDialogManager = new();
    private readonly IINACTIpc iinact;
    
    private int selectedGuildIndex;
    private int selectedRegionIndex;
    private int selectedVisibilityIndex;
    private object? syncedUser;

    private long SelectedGuildValue => plugin.FFLogs.User?.GuildSelectItems[selectedGuildIndex].Value ?? 0L;
    private long SelectedRegionValue => plugin.FFLogs.User?.RegionOrServerSelectItems[selectedRegionIndex].Value ?? 0L;
    private long SelectedVisibilityValue =>
        plugin.FFLogs.User?.ReportVisibilitySelectItems[selectedVisibilityIndex].Value ?? 0L;
    
    private string reportDescription = string.Empty;
    
    private bool AnyOperationInProgress => liveLoggingStatus == OperationStatus.InProgress
                                           || uploadALogStatus == OperationStatus.InProgress
                                           || splitALogStatus == OperationStatus.InProgress;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("FF Logs 上傳工具###FFLogsMainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        SizeCondition = ImGuiCond.FirstUseEver;
        
        this.plugin = plugin;
        iinact = new IINACTIpc(Plugin.PluginInterface);
        
        SetOptionsFromConfiguration();

        plugin.FFLogs.LiveLoggingStarted += OnLiveLoggingStarted;
        plugin.FFLogs.LiveLoggingReportCreated += OnLiveLoggingReportCreated;
        plugin.FFLogs.LiveLoggingProgress += OnLiveLoggingProgress;
        plugin.FFLogs.LiveLoggingEnded += OnLiveLoggingEnded;
    }

    public void Dispose()
    {
        plugin.FFLogs.LiveLoggingEnded -= OnLiveLoggingEnded;
        plugin.FFLogs.LiveLoggingProgress -= OnLiveLoggingProgress;
        plugin.FFLogs.LiveLoggingReportCreated -= OnLiveLoggingReportCreated;

        GC.SuppressFinalize(this);
    }

    private enum OperationStatus {
        Idle,
        InProgress,
    }

    public override void Draw()
    {
        if (plugin.FFLogs.User == null)
        {
            DrawLoginScreen();
            return;
        }

        // Automatic login happens in the background without going through DoLoginContinuation, so the combos and the
        // values saved in the configuration have to be synced against the new user's select items here.
        if (!ReferenceEquals(syncedUser, plugin.FFLogs.User))
        {
            syncedUser = plugin.FFLogs.User;
            SetOptionsFromConfiguration();
        }

        fileDialogManager.Draw();
        
        using var tabBar = ImRaii.TabBar("FFLogsTabs");
        if (tabBar.Success)
        {
            using (var liveLogTabItem = ImRaii.TabItem("即時紀錄###LiveLogTab"))
            {
                if (liveLogTabItem.Success)
                {
                    DrawLiveLogTab();
                }
            }

            using (var uploadALogTabItem = ImRaii.TabItem("上傳紀錄檔###UploadTab"))
            {
                if (uploadALogTabItem.Success)
                {
                    DrawUploadALogTab();
                }
            }

            using (var splitALogTabItem = ImRaii.TabItem("分割紀錄檔###SplitTab"))
            {
                if (splitALogTabItem.Success)
                {
                    DrawSplitALogTab();
                }
            }

            using (var settingsTabItem = ImRaii.TabItem("設定###SettingsTab"))
            {
                if (settingsTabItem.Success)
                {
                    DrawSettingsTab();
                }
            }
        }
    }

    private bool DrawParserStatus()
    {
        if (plugin.FFLogs.ParsersError is { } e)
        {
            var msg = e.InnerExceptions.FirstOrDefault(e).Message;
            
            ImGui.TextColored(ImGuiColors.DalamudRed, $"Parser 載入失敗，請查看 Dalamud 日誌 (/xllog)：{msg}");
            ImGui.TextColored(ImGuiColors.DalamudRed, "請停用後重新啟用外掛再試一次。");
            return false;
        } 
        
        if (!plugin.FFLogs.ParsersReady)
        {
            ImGui.Text("正在載入 parser...");
            return false;
        }

        return true;
    }
    
    private void DrawSharedUploadOptions()
    {
        var guildNames = plugin.FFLogs.User!.GuildSelectItems.Select(item => item.Label).ToArray();
        var regionNames = plugin.FFLogs.User!.RegionOrServerSelectItems.Select(item => item.Label).ToArray();
        var visibilityNames = plugin.FFLogs.User!.ReportVisibilitySelectItems.Select(item => item.Label).ToArray();
        
        ImGui.Text("上傳至公會：");
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##guild", ref selectedGuildIndex, guildNames))
        {
            plugin.Configuration.SelectedGuildValue = SelectedGuildValue;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();

        if (plugin.FFLogs.User!.GuildSelectItems[selectedGuildIndex].Value == -1)
        {
            ImGui.SetNextItemWidth(60);
            if (ImGui.Combo("##region", ref selectedRegionIndex, regionNames))
            {
                plugin.Configuration.SelectedRegionValue = SelectedRegionValue;
                plugin.Configuration.Save();
            }
            ImGui.SameLine();
        }
        
        ImGui.SetNextItemWidth(80);
        if (ImGui.Combo("##visibility", ref selectedVisibilityIndex, visibilityNames))
        {
            plugin.Configuration.SelectedVisibilityValue = SelectedVisibilityValue;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Text("報告說明：");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##description", ref reportDescription);
    }

    private static bool DrawActionButtonAndMessages(string buttonLabel, bool isButtonDisabled, string progressMessage, string errorMessage)
    {
        bool result;
        using (ImRaii.Disabled(isButtonDisabled))
        {
            result = ImGui.Button(buttonLabel);
        }
        
        if (!progressMessage.IsNullOrWhitespace())
        {
            ImGui.SameLine();
            ImGui.Text(progressMessage);
        }

        if (!errorMessage.IsNullOrWhitespace())
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), errorMessage);
        }

        return result;
    }

    private string GetDialogStartPath(string logFileOrFolder)
    {
        if (!logFileOrFolder.IsNullOrWhitespace())
        {
            if (File.Exists(logFileOrFolder)
                && Path.GetDirectoryName(logFileOrFolder) is { } folder
                && Directory.Exists(folder))
            {
                return folder;
            }

            if (Directory.Exists(logFileOrFolder))
            {
                return logFileOrFolder;
            }
        }

        if (iinact.IsActive() && iinact.GetLogFilePath() is { } iinactLogDirectory)
        {
            Plugin.Log.Debug("IINACT is active, opening file browser to IINACT log directory {0}", iinactLogDirectory);
            return iinactLogDirectory;
        }
        
        // General default locations for log files:
        // - %APPDATA%\Advanced Combat Tracker\FFXIVLogs
        // - Documents/IINACT

        var actFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Advanced Combat Tracker",
            "FFXIVLogs"
        );

        if (Directory.Exists(actFolder))
        {
            return actFolder;
        }

        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var iinactFolder = Path.Combine(documentsFolder, "IINACT");

        return Directory.Exists(iinactFolder) ? iinactFolder : documentsFolder;
    }
}
