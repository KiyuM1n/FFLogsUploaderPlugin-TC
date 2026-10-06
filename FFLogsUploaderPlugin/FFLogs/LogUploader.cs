using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using Newtonsoft.Json;
using Serilog.Events;

namespace FFLogsUploaderPlugin.FFLogs;

public class LogUploader(DesktopClient desktopClient, LogParser logParser)
{
    public class LogUploaderException(string message) : Exception(message);
    
    public int FightsUploaded { get; private set; }

    public async Task StartLiveLogAsync(
        string logFolder,
        long region,
        long visibility,
        long? guildId = null,
        string description = "",
        bool includeEntireFileInReport = false,
        IProgress<string>? progress = null,
        Action<string>? onReportCreated = null,
        CancellationToken token = default)
    {
        progress?.Report("即時紀錄已開始。");
        FightsUploaded = 0;
        await logParser.ClearAsync();
        
        progress?.Report("正在建立 FF Logs 報告。");
        var uploadTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var report = await desktopClient.CreateReportAsync(
                         await logParser.GetParserVersionAsync(),
                         uploadTime,
                         uploadTime,
                         guildId,
                         "live.log",
                         region,
                         visibility,
                         description,
                         token);
        onReportCreated?.Invoke(report.Code);

        await logParser.SetReportCodeAsync(report.Code);

        var segmentId = 1L;
        var logReader = new DirectoryLogReader(logFolder);

        Plugin.Log.Information("[LiveLog] Report {0} created: Region={1} Folder={2} LatestFile={3}",
                               report.Code, region, logFolder, logReader.CurrentFile ?? "(none)");
        
        if (logReader.CurrentFile is { } latestLogFile)
        {
            if (!includeEntireFileInReport)
            {
                var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                await logParser.SetLiveLoggingStartTimeAsync(t);
            }

            var fileInfo = new FileInfo(latestLogFile);
            var catchupAction = includeEntireFileInReport ? "上傳" : "解析";
            
            progress?.Report($"正在{catchupAction}最新紀錄檔 {Path.GetFileName(latestLogFile)} (0%)");
            
            // We intentionally don't use a cancellation token here because we check for cancellation every time
            // after a chunk is uploaded.
            // ReSharper disable once UseCancellationTokenForIAsyncEnumerable
            await foreach (var chunk in LogReader.ReadFileChunkedLinesAsync(latestLogFile))
            {
                // SetLiveLoggingStartTimeAsync above will stop previous logs from being uploaded.
                segmentId = await UploadLogPartAsync(report.Code, chunk.Lines, chunk.EndPosition, chunk.IsEof,
                                                     segmentId, region,
                                                     [], true, false, false);
                logReader.CurrentPosition = chunk.EndPosition;
                
                progress?.Report($"正在{catchupAction}最新紀錄檔 {Path.GetFileName(latestLogFile)} ({Math.Min(100, chunk.EndPosition * 100 / fileInfo.Length)}%, {chunk.EndPosition}/{Math.Max(chunk.EndPosition,fileInfo.Length)})");

                if (token.IsCancellationRequested)
                    break;
            }
        }
        
        Plugin.Log.Debug("Catch-up completed: LatestLogFile={0} CurrentPosition={1}",
                         logReader.CurrentFile, logReader.CurrentPosition);

        if (token.IsCancellationRequested)
        {
            await desktopClient.TerminateReport(report.Code);
            return;
        }

        Plugin.Log.Debug("Staring main live log watch loop");

        progress?.Report(logReader.CurrentFile != null
                             ? $"正在監看 {Path.GetFileName(logReader.CurrentFile)} 的新紀錄。"
                             : "等待紀錄檔中。超過 6 小時沒有寫入的紀錄檔不會用於即時紀錄。");

        FileInfo? latestFileInfo = null;
        var lastMeterCollection = DateTime.UtcNow;

        await foreach (var chunk in logReader.IterateChunksAsync(token: token))
        {
            // There are no latest log files in the directory.
            if (chunk == null)
            {
                if (token.IsCancellationRequested)
                {
                    if (latestFileInfo != null)
                        await UploadLogPartAsync(report.Code, [], latestFileInfo.Length, true, segmentId,
                                                 region, [], true, false, true);

                    break;
                }
                
                continue;
            }

            latestFileInfo = chunk.FileInfo;
            
            // Push fights if the log file has not changed for 120 seconds, or if cancellation is requested.
            var isIdleLogFile = DateTime.UtcNow.Subtract(chunk.FileInfo.LastWriteTimeUtc).TotalSeconds > 120;
            var pushFightIfNeeded = token.IsCancellationRequested || isIdleLogFile;
            
            if (pushFightIfNeeded)
                Plugin.Log.Debug("[LiveLog] PushFightIfNeeded={0} (CancellationRequested={1}, IdleLogFile={2})",
                                 pushFightIfNeeded, token.IsCancellationRequested, isIdleLogFile);
            
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                var joinedLines = string.Join("\n", chunk.Lines);
                joinedLines = joinedLines[..Math.Min(500, joinedLines.Length)];

                if (!joinedLines.IsNullOrWhitespace())
                {
                    Plugin.Log.Verbose(joinedLines[..Math.Min(500, joinedLines.Length)]);    
                }
            }
            
            if (chunk.Lines.Count > 0 || chunk.IsEof)
            {
                segmentId = await UploadLogPartAsync(report.Code, chunk.Lines, chunk.EndPosition, chunk.IsEof,
                                                     segmentId, region, [], true, false,
                                                     pushFightIfNeeded);
                progress?.Report($"正在上傳最新紀錄檔 {chunk.FileName} ({Math.Min(100, chunk.EndPosition * 100 / chunk.FileInfo.Length)}%, {chunk.EndPosition}/{Math.Max(chunk.EndPosition, chunk.FileInfo.Length)})，已上傳 {FightsUploaded} 場戰鬥");
            }

            if (token.IsCancellationRequested)
                break;
        }

        await desktopClient.TerminateReport(report.Code);
        progress?.Report("即時紀錄已結束。");
    }

    public async Task<string> UploadLogFileAsync(
        string logFilePath,
        long region,
        long visibility,
        long? guildId = null,
        string description = "",
        List<LogParser.ScannedRaid>? raidsToUpload = null,
        IProgress<string>? progress = null)
    {
        progress?.Report("開始上傳紀錄檔");
        await logParser.ClearAsync();
        
        progress?.Report("正在建立 FF Logs 報告");
        var uploadTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var report = await desktopClient.CreateReportAsync(
                         await logParser.GetParserVersionAsync(),
                         uploadTime,
                         uploadTime,
                         guildId,
                         Path.GetFileName(logFilePath),
                         region,
                         visibility,
                         description);
        var reportCode = report.Code;
        var segmentId = 1L;
        var logFileSize = new FileInfo(logFilePath).Length;
        
        await logParser.SetReportCodeAsync(reportCode);

        progress?.Report("正在上傳紀錄檔 (0%)");
        
        // The official uploader will process 5000 lines of the log file at a time, maximum 8MB per chunk.
        // ACT log files should not be 8MB per 5000 lines, so we don't care about that.
        await foreach (var chunk in LogReader.ReadFileChunkedLinesAsync(logFilePath))
        {
            segmentId = await UploadLogPartAsync(reportCode, chunk.Lines, chunk.EndPosition, chunk.IsEof, segmentId, region,
                                                 raidsToUpload ?? [], false, false, false);
            progress?.Report($"正在上傳紀錄檔 ({Math.Min(100, chunk.EndPosition * 100 / logFileSize)}%, {chunk.EndPosition}/{logFileSize})，已上傳 {FightsUploaded} 場戰鬥");
        }
        
        progress?.Report("正在完成 FF Logs 報告");

        await logParser.ClearAsync();
        await desktopClient.TerminateReport(reportCode);

        return reportCode;
    }

    private static string? FindLatestLogFileInFolder(string logFolder)
    {
        var latestLogFile = Directory.EnumerateFiles(logFolder, "Network_*.log", SearchOption.TopDirectoryOnly)
                 .OrderByDescending(File.GetLastWriteTimeUtc)
                 .FirstOrDefault();

        if (latestLogFile == null || DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(latestLogFile)).TotalHours >= 6)
            return null;

        return latestLogFile;
    }
    
    private async Task<long> UploadLogPartAsync(
        string reportCode, List<string> lines, long startPosition, bool isEof, long segmentId, long region,
        List<LogParser.ScannedRaid> raidsToUpload, bool isLiveLog, bool isRealTime, bool pushFightIfNeeded)
    {
        var result = await logParser.ParseLinesAsync(lines, region, raidsToUpload, false, startPosition);

        if (!result.Success)
        {
            Plugin.Log.Error($"[LogUploader] Failed to parse log line {result.ParsedLineCount}\n{result.Line}\n{JsonConvert.SerializeObject(result.Exception, Formatting.Indented)}");
            throw new LogUploaderException("解析紀錄時發生錯誤，請查看 Dalamud 日誌 (/xllog)");
        }

        var fightData = await logParser.CollectFightsAsync(
                            pushFightIfNeeded || (isEof && !isLiveLog),
                            false);
        var hasInProgressFight = false;

        if (isLiveLog && isRealTime && fightData.Fights.Count <= 0)
        {
            var inProgressFightData = await logParser.CollectInProgressFightAsync();

            hasInProgressFight = inProgressFightData.Fights.Count > 0;
            fightData = inProgressFightData;
        }

        Plugin.Log.Debug("[LogUploader] Lines={0} Position={1} Eof={2} PushFight={3} Fights={4} InProgress={5}",
                         lines.Count, startPosition, isEof, pushFightIfNeeded, fightData.Fights.Count,
                         hasInProgressFight);

        if (fightData.Fights.Count <= 0)
        {
            return segmentId;
        }

        var masterInfo = await logParser.CollectMasterInfoAsync(reportCode);

        if (!masterInfo.Success)
        {
            await logParser.ClearAsync();
            throw new LogUploaderException("無法從 parser 取得報告主資料");
        }

        var masterTable = BuildMasterTable(fightData.LogVersion, fightData.GameVersion,
                                           fightData.LogFileDetails, masterInfo);
        
        //Plugin.Log.Debug("[LogUploader] Master table: {0}", masterTable);

        try
        {
            await desktopClient.SetReportMasterTable(reportCode, segmentId, isRealTime, CompressStringIntoZipBlob(masterTable));
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Failed to set report master table");
            await logParser.ClearAsync();
            throw;
        }

        var fightsTable = BuildFightsTable(fightData.LogVersion, fightData.GameVersion, fightData.Fights);

        //Plugin.Log.Debug("[LogUploader] Fights table: {0}", fightsTable);

        DesktopClient.AddReportSegmentResponse addReportSegmentResponse;
        try
        {
            addReportSegmentResponse = await desktopClient.AddReportSegment(
                                           reportCode,
                                           CompressStringIntoZipBlob(fightsTable),
                                           fightData.StartTime,
                                           fightData.EndTime,
                                           fightData.Mythic,
                                           isLiveLog,
                                           isRealTime,
                                           (isRealTime && hasInProgressFight) ? fightData.Fights[0].EventCount : 0L,
                                           segmentId);
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Failed to add report segment");
            await logParser.ClearAsync();
            throw;
        }

        FightsUploaded += fightData.Fights.Count;

        await logParser.ClearFightsAsync();
        return addReportSegmentResponse.NextSegmentId;
    }

    private static string BuildMasterTable(
        long logVersion, long gameVersion, string logFileDetails, LogParser.CollectMasterInfoResponseData masterInfo)
    {
        var sb = new StringBuilder(
            logFileDetails.Length
            + masterInfo.ActorsString.Length
            + masterInfo.AbilitiesString.Length
            + masterInfo.TuplesString.Length
            + masterInfo.PetsString.Length
            + 7 + 114); // separator characters + numbers

        sb.Append(logVersion);
        sb.Append('|');
        sb.Append(gameVersion);
        sb.Append('|');
        sb.Append(logFileDetails);
        sb.Append('\n');

        sb.Append(masterInfo.LastAssignedActorId);
        sb.Append('\n');
        sb.Append(masterInfo.ActorsString);

        if (!masterInfo.ActorsString.EndsWith('\n'))
            sb.Append('\n');

        sb.Append(masterInfo.LastAssignedAbilityId);
        sb.Append('\n');
        sb.Append(masterInfo.AbilitiesString);
        
        if (!masterInfo.AbilitiesString.EndsWith('\n'))
            sb.Append('\n');
        
        sb.Append(masterInfo.LastAssignedTupleId);
        sb.Append('\n');
        sb.Append(masterInfo.TuplesString);
        
        if (!masterInfo.TuplesString.EndsWith('\n'))
            sb.Append('\n');
        
        sb.Append(masterInfo.LastAssignedPetId);
        sb.Append('\n');
        sb.Append(masterInfo.PetsString);
        
        if (!masterInfo.PetsString.EndsWith('\n'))
            sb.Append('\n');

        return sb.ToString();
    }

    private static string BuildFightsTable(long logVersion, long gameVersion, List<LogParser.Fight> fights)
    {
        var totalEvents = 0L;
        var eventsStringBuilder = new StringBuilder();

        foreach (var fight in fights)
        {
            totalEvents += fight.EventCount;
            eventsStringBuilder.Append(fight.EventsString);
        }

        return $"{logVersion}|{gameVersion}\n{totalEvents}\n{eventsStringBuilder}";
    }

    private static byte[] CompressStringIntoZipBlob(string data)
    {
        using var ms = new MemoryStream();
        using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create))
        {
            var entry = zipArchive.CreateEntry("log.txt", CompressionLevel.Fastest);
            using var sw = new StreamWriter(entry.Open());
        
            sw.Write(data);
        }
        
        return ms.ToArray();
    }
}
