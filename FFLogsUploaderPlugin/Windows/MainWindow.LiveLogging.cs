using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace FFLogsUploaderPlugin.Windows;

public partial class MainWindow
{
    private OperationStatus liveLoggingStatus = OperationStatus.Idle;
    
    private string logFolder = string.Empty;
    private bool includeEntireFileInReport;
    private string liveLogProgressMessage = string.Empty;
    private string liveLogErrorMessage = string.Empty;
    private string liveLogReportCode = string.Empty;
    
    private void DrawLiveLogTab()
    {
        if (!DrawParserStatus())
            return;
        
        using (ImRaii.Disabled(AnyOperationInProgress))
        {
            ImGui.Spacing();
            ImGui.Text("ACT / IINACT 紀錄檔資料夾：");
        
            ImGui.SetNextItemWidth(-80);
            if (ImGui.InputText("##logFolder", ref logFolder))
            {
                plugin.Configuration.LiveLogFolder = logFolder;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && engageTimerPerPhase)
            {
                plugin.FFLogs.StopMetersLogCollection();
                plugin.FFLogs.StartMetersLogCollectionAsync();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("瀏覽##browseLogFolder"))
                fileDialogManager.OpenFolderDialog("選擇紀錄檔資料夾",
                                                   (success, path) =>
                                                   {
                                                       if (!success || path.IsNullOrWhitespace())
                                                           return;

                                                       logFolder = path;
                                                       plugin.Configuration.LiveLogFolder = logFolder;
                                                       plugin.Configuration.Save();
                                                       
                                                       if (engageTimerPerPhase)
                                                       {
                                                           plugin.FFLogs.StopMetersLogCollection();
                                                           plugin.FFLogs.StartMetersLogCollectionAsync();
                                                       }
                                                   },
                                                   GetDialogStartPath(logFolder));
        
            ImGui.Spacing();
            DrawSharedUploadOptions();

            ImGui.Spacing();
            if (ImGui.Checkbox("將整個紀錄檔加入報告", ref includeEntireFileInReport))
            {
                plugin.Configuration.IncludeEntireFileInReport = includeEntireFileInReport;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("從頭上傳最新的紀錄檔；未勾選時，只會上傳開始即時紀錄之後\n新增的內容。");
            }
        }

        // Keep this interactable if live logging is active so the user can stop it.
        ImGui.Spacing();
        if (DrawActionButtonAndMessages(
                plugin.FFLogs.IsLiveLogging ? "停止" : "開始",
                uploadALogStatus == OperationStatus.InProgress || splitALogStatus == OperationStatus.InProgress,
                liveLogProgressMessage,
                liveLogErrorMessage)
            )
        {
            if (plugin.FFLogs.IsLiveLogging)
                plugin.FFLogs.StopLiveLogging();
            else
                plugin.FFLogs.StartLiveLoggingAsync(reportDescription, includeEntireFileInReport);
        }
        
        if (!liveLogReportCode.IsNullOrWhitespace())
        {
            ImGui.Spacing();
            ImGui.Text("報告已建立。");
            
            ImGui.SameLine();
            if (ImGui.Button("複製報告連結"))
                ImGui.SetClipboardText($"https://cn.fflogs.com/reports/{liveLogReportCode}");

            ImGui.SameLine();
            if (ImGui.Button("開啟報告"))
                Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://cn.fflogs.com/reports/{liveLogReportCode}", UseShellExecute = true
                }));

            ImGui.SameLine();
            if (ImGui.Button("開啟 XIVAnalysis"))
                Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://xivanalysis.com/fflogs/{liveLogReportCode}", UseShellExecute = true
                }));
        }
    }

    private void OnLiveLoggingStarted(object? sender, EventArgs args)
    {
        liveLoggingStatus = OperationStatus.InProgress;
    }

    private void OnLiveLoggingProgress(object? sender, string progress)
    {
        liveLogProgressMessage = progress;
    }

    private void OnLiveLoggingReportCreated(object? sender, string reportCode)
    {
        liveLogReportCode = reportCode;
    }

    private void OnLiveLoggingEnded(object? sender, AggregateException? exception)
    {
        liveLoggingStatus = OperationStatus.Idle;

        if (exception == null)
            return;
        
        Plugin.Log.Error(exception, "Live logging operation failed");
        liveLogProgressMessage = string.Empty;
        liveLogErrorMessage = exception.InnerExceptions.FirstOrDefault(exception).Message;
    }
}
