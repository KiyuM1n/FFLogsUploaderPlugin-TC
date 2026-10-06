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
    private OperationStatus uploadALogStatus = OperationStatus.Idle;

    private string logFilePath = string.Empty;
    //private bool selectFightsToUpload;
    private string uploadALogProgressMessage = string.Empty;
    private Progress<string>? uploadALogProgress; 
    private string uploadALogErrorMessage = string.Empty;
    private string uploadALogReportCode = string.Empty;

    private void DrawUploadALogTab()
    {
        if (!DrawParserStatus())
            return;
        
        using (ImRaii.Disabled(AnyOperationInProgress))
        {
            ImGui.Spacing();
            ImGui.Text("要上傳的紀錄檔：");
        
            ImGui.SetNextItemWidth(-80);
            ImGui.InputText("##logFile", ref logFilePath);
            ImGui.SameLine();
            if (ImGui.Button("瀏覽##browseLogFile"))
                fileDialogManager.OpenFileDialog("選擇紀錄檔",
                                                 "紀錄檔{.log},所有檔案{.*}",
                                                 (success, paths) =>
                                                 {
                                                     if (success && paths.Count > 0)
                                                         logFilePath = paths[0];

                                                     plugin.Configuration.LogFilePath = logFilePath;
                                                     plugin.Configuration.Save();
                                                 },
                                                 1,
                                                 GetDialogStartPath(logFilePath));

            ImGui.Spacing();
            DrawSharedUploadOptions();

            ImGui.Spacing();
            // TODO
            // if (ImGui.Button("Select fights to upload"))
            // {
            //     
            // }
            //
            // ImGui.SameLine();
        }
        
        if (DrawActionButtonAndMessages("上傳", AnyOperationInProgress, uploadALogProgressMessage, uploadALogErrorMessage))
            DoUploadLogFile();

        if (!uploadALogReportCode.IsNullOrWhitespace())
        {
            ImGui.Spacing();
            ImGui.Text("紀錄檔上傳完成！");
            
            ImGui.SameLine();
            if (ImGui.Button("複製報告連結"))
                ImGui.SetClipboardText($"https://cn.fflogs.com/reports/{uploadALogReportCode}");

            ImGui.SameLine();
            if (ImGui.Button("開啟報告"))
                Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://cn.fflogs.com/reports/{uploadALogReportCode}", UseShellExecute = true
                }));
            
            ImGui.SameLine();
            if (ImGui.Button("開啟 XIVAnalysis"))
                Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://xivanalysis.com/fflogs/{uploadALogReportCode}", UseShellExecute = true
                }));
        }
    }

    private void DoUploadLogFile()
    {
        uploadALogStatus = OperationStatus.InProgress;
        uploadALogReportCode = string.Empty;
        uploadALogProgressMessage = string.Empty;
        uploadALogErrorMessage = string.Empty;
        
        if (logFilePath.IsNullOrWhitespace())
        {
            uploadALogStatus = OperationStatus.Idle;
            uploadALogErrorMessage = "請指定紀錄檔路徑。";
            return;
        }
        
        if (!File.Exists(logFilePath))
        {
            uploadALogStatus = OperationStatus.Idle;
            uploadALogErrorMessage = "紀錄檔不存在，或不是檔案。";
            return;
        }
        
        var guildId = SelectedGuildValue;
        var visibility = SelectedVisibilityValue;
        var region = SelectedRegionValue;

        if (uploadALogProgress == null)
        {
            uploadALogProgress = new Progress<string>();
            uploadALogProgress.ProgressChanged += OnUploadLogProgress;
        }

        Task.Run(() => plugin.FFLogs.UploadLogFileAsync(logFilePath, region, visibility, guildId == -1 ? null : guildId,
                                                   reportDescription, [], uploadALogProgress))
            .ContinueWith(task =>
            {
                uploadALogStatus = OperationStatus.Idle;
                uploadALogProgressMessage = string.Empty;
                
                if (task.Exception != null)
                {
                    Plugin.Log.Error(task.Exception, "Failed to upload log");
                    uploadALogErrorMessage = task.Exception.InnerExceptions.FirstOrDefault(task.Exception).Message;
                }
                else
                    uploadALogReportCode = task.Result;
            });
    }

    private void OnUploadLogProgress(object? sender, string progress)
    {
        uploadALogProgressMessage = progress;
    }
}
