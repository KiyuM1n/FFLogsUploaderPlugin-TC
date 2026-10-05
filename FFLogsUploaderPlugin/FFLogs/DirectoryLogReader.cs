using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FFLogsUploaderPlugin.Extensions;

namespace FFLogsUploaderPlugin.FFLogs;

internal class DirectoryLogReader
{
    public string LogDirectory { get; init; }
    public string? CurrentFile { get; private set; }
    public long CurrentPosition { get; set; }

    public DirectoryLogReader(string logDirectory)
    {
        LogDirectory = logDirectory;
        CurrentFile = FindLatestLogFile();
    }
    
    /// <summary>
    /// Iterate log lines in chunks, delaying between each chunk for 500ms.
    /// </summary>
    /// <param name="maxLines">The maximum number of lines in a chunk.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<FileChunk?> IterateChunksAsync(
        int maxLines = 5000, [EnumeratorCancellation] CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            var latestFile = FindLatestLogFile();

            if (latestFile != null && latestFile != CurrentFile)
            {
                // A newer log file has appeared. Read the current one to the end before switching over to it.
                if (CurrentFile != null)
                {
                    // We manually check cancellation.
                    // ReSharper disable once UseCancellationTokenForIAsyncEnumerable
                    await foreach (var chunk in ReadCurrentFileChunks(maxLines))
                    {
                        CurrentPosition = chunk.EndPosition;
                        yield return chunk;

                        if (token.IsCancellationRequested)
                            yield break;
                    }
                }

                CurrentFile = latestFile;
                CurrentPosition = 0L;
            }

            if (CurrentFile == null)
            {
                yield return null;

                if (await Task.DelayOrCancel(TimeSpan.FromSeconds(1), token))
                    yield break;
                
                continue;
            }

            // We manually check cancellation.
            // ReSharper disable once UseCancellationTokenForIAsyncEnumerable
            await foreach (var chunk in ReadCurrentFileChunks(maxLines))
            {
                CurrentPosition = chunk.EndPosition;
                yield return chunk;
            }

            if (await Task.DelayOrCancel(TimeSpan.FromMilliseconds(500), token))
                yield break;
        }
    }

    private async IAsyncEnumerable<FileChunk> ReadCurrentFileChunks(int maxLines)
    {
        ArgumentNullException.ThrowIfNull(CurrentFile);

        var fileInfo = new FileInfo(CurrentFile);
        var fileName = Path.GetFileName(CurrentFile);

        await foreach (var chunk in LogReader.ReadFileChunkedLinesAsync(CurrentFile, maxLines, CurrentPosition))
        {
            yield return new FileChunk
            {
                EndPosition = chunk.EndPosition,
                IsEof = chunk.IsEof,
                Lines = chunk.Lines,
                FileName = fileName,
                FileInfo = fileInfo,
            };
        }
    }

    public string? FindLatestLogFile()
    {
        var latestLogFile = Directory.EnumerateFiles(LogDirectory, "Network_*.log", SearchOption.TopDirectoryOnly)
                                     .OrderByDescending(File.GetLastWriteTimeUtc)
                                     .FirstOrDefault();

        if (latestLogFile == null || DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(latestLogFile)).TotalHours >= 6)
            return null;

        return latestLogFile;
    }

    internal class FileChunk
    {
        public required long EndPosition;
        public required bool IsEof;
        public required List<string> Lines;
        public required string FileName;
        public required FileInfo FileInfo;
    }
}
