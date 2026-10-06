using System;
using System.Threading;
using System.Threading.Tasks;

namespace FFLogsUploaderPlugin.Extensions;

public static class TaskExtensions
{
    public static async Task<bool> DelayOrCancel(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }
}
