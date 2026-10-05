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
            ImGui.Text("Folder ACT writes log files to:");
        
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
            if (ImGui.Button("Browse##browseLogFolder"))
                fileDialogManager.OpenFolderDialog("Select Log Folder",
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
            if (ImGui.Checkbox("Include entire file in report", ref includeEntireFileInReport))
            {
                plugin.Configuration.IncludeEntireFileInReport = includeEntireFileInReport;
                plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Uploads the latest log file from the beginning; otherwise, only logs added\nsince starting live logging will be uploaded.");
            }
        }

        // Keep this interactable if live logging is active so the user can stop it.
        ImGui.Spacing();
        if (DrawActionButtonAndMessages(
                plugin.FFLogs.IsLiveLogging ? "Stop" : "Start",
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
            ImGui.Text("Report created.");
            
            ImGui.SameLine();
            if (ImGui.Button("Copy report link"))
                ImGui.SetClipboardText($"https://www.fflogs.com/reports/{liveLogReportCode}");

            ImGui.SameLine();
            if (ImGui.Button("Open report link"))
                Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.fflogs.com/reports/{liveLogReportCode}", UseShellExecute = true
                }));

            ImGui.SameLine();
            if (ImGui.Button("Open XIVAnalysis"))
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
