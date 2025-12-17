using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using BackupApp.Core.Services;
using Moq;
using Xunit;

namespace BackupApp.Tests.Services;

public class RetentionServiceTests
{
    private readonly Mock<IBackupTaskRepository> _repositoryMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<RetentionService>> _loggerMock;
    private readonly RetentionService _retentionService;

    public RetentionServiceTests()
    {
        _repositoryMock = new Mock<IBackupTaskRepository>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RetentionService>>();
        _retentionService = new RetentionService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetVersionsToDelete_KeepAll_ReturnsEmpty()
    {
        // Arrange
        var histories = CreateTestHistories(5);
        var policy = RetentionPolicyType.KeepAll;

        // Act
        var result = _retentionService.GetVersionsToDelete(histories, policy, null, null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetVersionsToDelete_MaxVersions_KeepsOnlyNLatest()
    {
        // Arrange
        var histories = CreateTestHistories(10);
        var policy = RetentionPolicyType.MaxVersions;
        var maxVersions = 3;

        // Act
        var result = _retentionService.GetVersionsToDelete(histories, policy, maxVersions, null);

        // Assert
        Assert.Equal(7, result.Count); // Должно удалить 7 старых версий
        Assert.All(result, h => Assert.True(histories.IndexOf(h) >= maxVersions));
    }

    [Fact]
    public void GetVersionsToDelete_MaxAge_DeletesOldVersions()
    {
        // Arrange
        var histories = new List<BackupHistory>
        {
            CreateHistory(DateTime.UtcNow.AddDays(-1)), // Новый
            CreateHistory(DateTime.UtcNow.AddDays(-5)), // Старый
            CreateHistory(DateTime.UtcNow.AddDays(-10)), // Очень старый
            CreateHistory(DateTime.UtcNow.AddDays(-20)) // Очень старый
        };
        var policy = RetentionPolicyType.MaxAge;
        var maxAgeDays = 7;

        // Act
        var result = _retentionService.GetVersionsToDelete(histories, policy, null, maxAgeDays);

        // Assert
        Assert.Equal(2, result.Count); // Должно удалить 2 старых версии
        Assert.All(result, h => Assert.True(h.StartTime < DateTime.UtcNow.AddDays(-maxAgeDays)));
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_KeepAll_DoesNothing()
    {
        // Arrange
        var task = new BackupTask
        {
            Id = 1,
            Name = "TestTask",
            RetentionPolicyType = RetentionPolicyType.KeepAll
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        await _retentionService.ApplyRetentionPolicyAsync(1);

        // Assert
        _repositoryMock.Verify(r => r.DeleteHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_TaskNotFound_DoesNothing()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackupTask?)null);

        // Act
        await _retentionService.ApplyRetentionPolicyAsync(1);

        // Assert
        _repositoryMock.Verify(r => r.DeleteHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CountVersionsAsync_ReturnsCorrectCount()
    {
        // Arrange
        var task = new BackupTask
        {
            Id = 1,
            History = CreateTestHistories(5)
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var count = await _retentionService.CountVersionsAsync(1);

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task CountVersionsAsync_TaskNotFound_ReturnsZero()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackupTask?)null);

        // Act
        var count = await _retentionService.CountVersionsAsync(1);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountVersionsAsync_NullHistory_ReturnsZero()
    {
        // Arrange
        var task = new BackupTask
        {
            Id = 1,
            History = null!
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var count = await _retentionService.CountVersionsAsync(1);

        // Assert
        Assert.Equal(0, count);
    }

    private List<BackupHistory> CreateTestHistories(int count)
    {
        var histories = new List<BackupHistory>();
        for (int i = 0; i < count; i++)
        {
            histories.Add(CreateHistory(DateTime.UtcNow.AddDays(-i)));
        }
        return histories.OrderByDescending(h => h.StartTime).ToList();
    }

    private BackupHistory CreateHistory(DateTime startTime)
    {
        return new BackupHistory
        {
            Id = Guid.NewGuid().GetHashCode(),
            StartTime = startTime,
            EndTime = startTime.AddMinutes(5),
            Status = BackupStatus.Success,
            BackupType = BackupType.Full,
            FilesCopied = 10,
            TotalSize = 1000,
            OutputArtifactPath = $"C:\\Test\\Backup_{startTime:yyyyMMdd_HHmmss}"
        };
    }
}

