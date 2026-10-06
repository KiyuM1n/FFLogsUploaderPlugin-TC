using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFLogsUploaderPlugin.FFLogs;

namespace FFLogsUploaderPlugin.Windows;

public partial class MainWindow
{
    private string email = string.Empty;
    private string password = string.Empty;
    private bool automaticLogin;
    
    private string loginErrorMessage = string.Empty;
    
    private void DrawLoginScreen()
    {
        ImGui.Text("登入 FF Logs");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(plugin.FFLogs.IsLoggingIn))
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("電子郵件##email", "電子郵件", ref email,
                                        flags: ImGuiInputTextFlags.EnterReturnsTrue))
            {
                DoLogin();
            }
        
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("密碼##password", "密碼", ref password,
                                        flags: ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                DoLogin();
            }
        
            ImGui.Checkbox("自動登入", ref automaticLogin);
        
            ImGui.Spacing();

            if (ImGui.Button(plugin.FFLogs.IsLoggingIn ? "登入中..." : "登入", new Vector2(-1, 30)))
            {
                DoLogin();
            }
        }

        if (plugin.FFLogs.LoginError is { } e)
            loginErrorMessage = e.InnerExceptions.FirstOrDefault(e).Message;

        if (!loginErrorMessage.IsNullOrWhitespace())
        {
            ImGui.Spacing();
            ImGui.TextColored(ImGuiColors.DalamudRed, loginErrorMessage);
        }
    }

    private void DoLogin()
    {
        if (email.IsNullOrWhitespace() || password.IsNullOrWhitespace())
        {
            loginErrorMessage = "請輸入電子郵件和密碼。";
            return;
        }
        
        plugin.FFLogs.LoginAsync(email, password, automaticLogin).ContinueWith(DoLoginContinuation!);
    }

    private void DoLoginContinuation(Task<DesktopClient.LoginResponse?> task)
    {
        if (task.Exception != null)
        {
            Plugin.Log.Error(task.Exception, "Log in failed");
            loginErrorMessage = task.Exception.InnerExceptions.FirstOrDefault(task.Exception).Message;
            return;
        }

        if (task.Result is not { } user)
            return;
                
        Plugin.Log.Information("Logged in as {0}", user.User.UserName);
        SetOptionsFromConfiguration();
        plugin.FFLogs.StartParsersAsync(false, true, true);
    }
}
