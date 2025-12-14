using System;

namespace BackupApp.Core.Models;

public sealed class BackupProgressReport
{
    public BackupProgressReport(long totalBytes, long processedBytes, int filesProcessed, string currentFile, double bytesPerSecond)
    {
        TotalBytes = totalBytes;
        ProcessedBytes = processedBytes;
        FilesProcessed = filesProcessed;
        CurrentFile = currentFile;
        BytesPerSecond = bytesPerSecond;
        Percentage = totalBytes <= 0 ? 100 : Math.Clamp((double)processedBytes / totalBytes * 100d, 0d, 100d);

        if (bytesPerSecond > 0 && totalBytes > processedBytes)
        {
            var remainingSeconds = (totalBytes - processedBytes) / bytesPerSecond;
            EstimatedRemaining = TimeSpan.FromSeconds(remainingSeconds);
        }
    }

    public long TotalBytes { get; }

    public long ProcessedBytes { get; }

    public int FilesProcessed { get; }

    public string CurrentFile { get; }

    public double BytesPerSecond { get; }

    public double Percentage { get; }

    public TimeSpan? EstimatedRemaining { get; }
}






