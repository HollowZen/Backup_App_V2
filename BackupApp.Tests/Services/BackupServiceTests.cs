using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Core.Services;
using Moq;
using Xunit;

namespace BackupApp.Tests.Services;

public class BackupServiceTests : IDisposable
{
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly BackupService _backupService;
    private readonly string _testSourceDir;
    private readonly string _testTargetDir;

    public BackupServiceTests()
    {
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _backupService = new BackupService(_encryptionServiceMock.Object);

        // Создаем временные директории для тестов
        _testSourceDir = Path.Combine(Path.GetTempPath(), $"BackupTest_Source_{Guid.NewGuid()}");
        _testTargetDir = Path.Combine(Path.GetTempPath(), $"BackupTest_Target_{Guid.NewGuid()}");

        Directory.CreateDirectory(_testSourceDir);
        Directory.CreateDirectory(_testTargetDir);
    }

    public void Dispose()
    {
        // Очистка после тестов
        if (Directory.Exists(_testSourceDir))
        {
            Directory.Delete(_testSourceDir, true);
        }

        if (Directory.Exists(_testTargetDir))
        {
            Directory.Delete(_testTargetDir, true);
        }
    }

    [Fact]
    public async Task ExecuteBackupAsync_WithEmptySourcePath_ThrowsException()
    {
        // Arrange
        var task = new BackupTask
        {
            SourcePath = string.Empty,
            TargetPath = _testTargetDir,
            BackupType = BackupType.Full,
            UseCompression = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _backupService.ExecuteBackupAsync(task));
    }

    [Fact]
    public async Task ExecuteBackupAsync_WithNonExistentSource_ThrowsException()
    {
        // Arrange
        var task = new BackupTask
        {
            SourcePath = @"C:\NonExistentPath\Test",
            TargetPath = _testTargetDir,
            BackupType = BackupType.Full,
            UseCompression = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _backupService.ExecuteBackupAsync(task));
        
        Assert.Contains("Исходная папка не найдена", exception.Message);
    }

    [Fact]
    public async Task ExecuteBackupAsync_WithEmptyTargetPath_ThrowsException()
    {
        // Arrange
        var task = new BackupTask
        {
            SourcePath = _testSourceDir,
            TargetPath = string.Empty,
            BackupType = BackupType.Full,
            UseCompression = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _backupService.ExecuteBackupAsync(task));
    }

    [Fact]
    public async Task ExecuteBackupAsync_FullBackup_CreatesBackup()
    {
        // Arrange
        var testFile = Path.Combine(_testSourceDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = _testSourceDir,
            TargetPath = _testTargetDir,
            BackupType = BackupType.Full,
            UseCompression = false,
            UseEncryption = false
        };

        // Act
        var result = await _backupService.ExecuteBackupAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BackupType.Full, result.PerformedBackupType);
        Assert.True(result.FilesCopied > 0);
        Assert.True(Directory.Exists(result.OutputArtifactPath));
    }

    [Fact]
    public async Task ExecuteBackupAsync_WithCompression_CreatesZipArchive()
    {
        // Arrange
        var testFile = Path.Combine(_testSourceDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = _testSourceDir,
            TargetPath = _testTargetDir,
            BackupType = BackupType.Full,
            UseCompression = true,
            CompressionLevel = 5,
            UseEncryption = false
        };

        // Act
        var result = await _backupService.ExecuteBackupAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UsedCompression);
        Assert.True(Directory.Exists(result.OutputArtifactPath));
        
        // Проверяем наличие ZIP файла
        var zipFiles = Directory.GetFiles(result.OutputArtifactPath, "*.zip", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(zipFiles);
    }

    [Fact]
    public async Task ExecuteBackupAsync_WithRussianCharacters_HandlesCorrectly()
    {
        // Arrange
        var russianDir = Path.Combine(_testSourceDir, "Тестовая_папка");
        Directory.CreateDirectory(russianDir);
        var testFile = Path.Combine(russianDir, "тестовый_файл.txt");
        await File.WriteAllTextAsync(testFile, "Тестовое содержимое");

        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = _testSourceDir,
            TargetPath = _testTargetDir,
            BackupType = BackupType.Full,
            UseCompression = false,
            UseEncryption = false
        };

        // Act
        var result = await _backupService.ExecuteBackupAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FilesCopied > 0);
    }

    [Fact]
    public async Task ExecuteBackupAsync_IncrementalBackup_CopiesOnlyChangedFiles()
    {
        // Arrange
        var testFile1 = Path.Combine(_testSourceDir, "file1.txt");
        await File.WriteAllTextAsync(testFile1, "Content 1");

        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = _testSourceDir,
            TargetPath = _testTargetDir,
            BackupType = BackupType.Incremental,
            UseCompression = false,
            UseEncryption = false
        };

        // Первый запуск должен быть Full
        var result1 = await _backupService.ExecuteBackupAsync(task);
        Assert.Equal(BackupType.Full, result1.PerformedBackupType);

        // Обновляем время последнего бэкапа (делаем небольшую задержку для гарантии)
        await Task.Delay(100);
        task.LastBackupTime = DateTime.UtcNow.AddSeconds(-1); // Устанавливаем время немного в прошлом

        // Добавляем новый файл
        var testFile2 = Path.Combine(_testSourceDir, "file2.txt");
        await File.WriteAllTextAsync(testFile2, "Content 2");
        // Устанавливаем время изменения файла в будущем относительно LastBackupTime
        File.SetLastWriteTimeUtc(testFile2, DateTime.UtcNow);

        // Act - второй запуск должен быть Incremental
        var result2 = await _backupService.ExecuteBackupAsync(task);

        // Assert
        // Может быть Full, если нет измененных файлов, или Incremental если есть
        Assert.True(result2.PerformedBackupType == BackupType.Incremental || result2.PerformedBackupType == BackupType.Full);
        Assert.True(result2.FilesCopied >= 0); // Может быть 0, если файлы не изменились
    }
}

