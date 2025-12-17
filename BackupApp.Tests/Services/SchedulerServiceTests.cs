using System;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using BackupApp.Core.Services;
using Moq;
using Xunit;

namespace BackupApp.Tests.Services;

public class SchedulerServiceTests : IDisposable
{
    private readonly Mock<IBackupTaskRepository> _repositoryMock;
    private readonly Mock<IBackupService> _backupServiceMock;
    private readonly Mock<IRetentionService> _retentionServiceMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<SchedulerService>> _loggerMock;
    private readonly SchedulerService _schedulerService;

    public SchedulerServiceTests()
    {
        _repositoryMock = new Mock<IBackupTaskRepository>();
        _backupServiceMock = new Mock<IBackupService>();
        _retentionServiceMock = new Mock<IRetentionService>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<SchedulerService>>();

        _schedulerService = new SchedulerService(
            _repositoryMock.Object,
            _backupServiceMock.Object,
            _retentionServiceMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _schedulerService?.Dispose();
    }

    [Fact]
    public void CalculateNextRun_ManualSchedule_ReturnsNull()
    {
        // Arrange
        var task = new BackupTask
        {
            ScheduleType = ScheduleType.Manual,
            ScheduleTime = null
        };

        // Act
        var result = _schedulerService.CalculateNextRun(task);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateNextRun_DailySchedule_ReturnsNextDay()
    {
        // Arrange
        var now = DateTime.Now;
        var task = new BackupTask
        {
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "10:00"
        };

        // Act
        var result = _schedulerService.CalculateNextRun(task, now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Value.Hour);
        Assert.Equal(0, result.Value.Minute);
    }

    [Fact]
    public void CalculateNextRun_WeeklySchedule_ReturnsNextWeekday()
    {
        // Arrange
        var now = DateTime.Now;
        var task = new BackupTask
        {
            ScheduleType = ScheduleType.Weekly,
            ScheduleTime = "10:00",
            ScheduleDays = "Mon"
        };

        // Act
        var result = _schedulerService.CalculateNextRun(task, now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Value.Hour);
    }

    [Fact]
    public void CalculateNextRun_MonthlySchedule_ReturnsNextMonthDay()
    {
        // Arrange
        var now = DateTime.Now;
        var task = new BackupTask
        {
            ScheduleType = ScheduleType.Monthly,
            ScheduleTime = "10:00",
            ScheduleDayOfMonth = 15
        };

        // Act
        var result = _schedulerService.CalculateNextRun(task, now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15, result.Value.Day);
        Assert.Equal(10, result.Value.Hour);
    }

    [Fact]
    public void StartScheduler_StartsScheduler()
    {
        // Act
        _schedulerService.StartScheduler();

        // Assert
        Assert.True(_schedulerService.IsRunning);
    }

    [Fact]
    public void StopScheduler_StopsScheduler()
    {
        // Arrange
        _schedulerService.StartScheduler();

        // Act
        _schedulerService.StopScheduler();

        // Assert
        Assert.False(_schedulerService.IsRunning);
    }
}

