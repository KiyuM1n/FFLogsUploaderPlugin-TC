using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace FFLogsUploaderPlugin.Windows;

public partial class MainWindow
{
    private OperationStatus splitALogStatus = OperationStatus.Idle;
    
    private string logFilePathToSplit = string.Empty;
    private string splitLogProgressMessage = string.Empty;
    private string splitLogErrorMessage = string.Empty;
    private bool splitLogGroupSameContent = false;
    
    private void DrawSplitALogTab()
    {
        using (ImRaii.Disabled(AnyOperationInProgress))
        {
            ImGui.Spacing();
            ImGui.Text("要分割的紀錄檔：");
            
            ImGui.SetNextItemWidth(-80);
            ImGui.InputText("##logFileToSplit", ref logFilePathToSplit);
            ImGui.SameLine();
            if (ImGui.Button("瀏覽##browseLogFileToSplit"))
                fileDialogManager.OpenFileDialog("選擇紀錄檔",
                                                 "紀錄檔{.log},所有檔案{.*}",
                                                 (success, paths) =>
                                                 {
                                                     if (success && paths.Count > 0)
                                                         logFilePathToSplit = paths[0];
                                                 },
                                                 1,
                                                 GetDialogStartPath(logFilePathToSplit));

            if (ImGui.Checkbox("依副本內容分割", ref splitLogGroupSameContent))
            {
                plugin.Configuration.SplitLogGroupSameContent = splitLogGroupSameContent;
                plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker(
                "預設會在每次切換區域時分割。例如紀錄檔依序是 副本 A -> 利姆薩 -> 副本 A -> 利姆薩 -> 副本 B 時，預設會分成 5 個檔案（每個區域一個）；勾選後只會分成 2 個：副本 A 一個、副本 B 一個。");
        }
        
        ImGui.Spacing();

        if (DrawActionButtonAndMessages("分割", AnyOperationInProgress, splitLogProgressMessage, splitLogErrorMessage))
            DoSplitLogFile();
    }
    
    private void DoSplitLogFile()
    {
        splitALogStatus = OperationStatus.InProgress;
        splitLogProgressMessage = string.Empty;
        splitLogErrorMessage = string.Empty;
     
        if (logFilePathToSplit.IsNullOrWhitespace())
        {
            splitALogStatus = OperationStatus.Idle;
            splitLogErrorMessage = "請指定紀錄檔路徑。";
            return;
        }
     
        if (Path.GetFileName(logFilePathToSplit).StartsWith("Split-"))
        {
            splitALogStatus = OperationStatus.Idle;
            splitLogErrorMessage = "這已經是分割過的紀錄檔，不會再分割。";
            return;
        }
     
        if (!File.Exists(logFilePathToSplit))
        {
            splitALogStatus = OperationStatus.Idle;
            splitLogErrorMessage = "紀錄檔不存在，或不是檔案。";
            return;
        }
     
        FFLogsManager.SplitLogFileAsync(logFilePathToSplit, splitLogGroupSameContent).ContinueWith(task =>
        {
            splitALogStatus = OperationStatus.Idle;
     
            if (task.Exception?.InnerExceptions.FirstOrDefault() is SplitLogException sle)
            {
                splitLogProgressMessage = string.Empty;
                splitLogErrorMessage = sle.Message;
            }
            else if (task.Exception != null)
            {
                Plugin.Log.Error(task.Exception, "Split log file failed");
                splitLogProgressMessage = string.Empty;
                splitLogErrorMessage = task.Exception.InnerExceptions.FirstOrDefault(task.Exception).Message;
            }
            else
            {
                splitLogProgressMessage = "紀錄檔分割完成";
            }
        });
    }
}
