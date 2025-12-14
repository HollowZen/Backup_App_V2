using System;

namespace BackupApp.Core.Models;

public enum BackupStatus
{
    Success = 0,
    Failed = 1,
    Canceled = 2
}

public class BackupHistory
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public BackupTask? Task { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public BackupStatus Status { get; set; }

    public BackupType BackupType { get; set; }

    public bool UsedCompression { get; set; }

    public int CompressionLevel { get; set; }

    public int FilesCopied { get; set; }

    public long TotalSize { get; set; }

    public string? OutputArtifactPath { get; set; }

    public long CompressedSize { get; set; }

    public TimeSpan Duration { get; set; }
}




















