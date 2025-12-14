using System;

namespace BackupApp.Core.Models;

public sealed class BackupExecutionResult
{
    public BackupExecutionResult(string sourcePath, string targetPath)
    {
        SourcePath = sourcePath;
        TargetPath = targetPath;
        StartedAt = DateTime.UtcNow;
    }

    public string SourcePath { get; }

    public string TargetPath { get; }

    public int FilesCopied { get; internal set; }

    public int DirectoriesCreated { get; internal set; }

    public long TotalBytes { get; internal set; }

    public long FilesSkipped { get; internal set; }

    public long BytesWritten { get; internal set; }

    public BackupType PerformedBackupType { get; internal set; } = BackupType.Full;

    public bool UsedCompression { get; internal set; }

    public int CompressionLevel { get; internal set; }

    public string? OutputArtifactPath { get; internal set; }

    public TimeSpan Duration { get; internal set; }

    public DateTime StartedAt { get; internal set; }

    public DateTime CompletedAt { get; internal set; }
}

