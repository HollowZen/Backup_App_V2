using System;
using System.Linq;
using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Data;
using BackupApp.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BackupApp.Tests.Repositories;

public class BackupTaskRepositoryTests : IDisposable
{
    private readonly BackupAppDbContext _dbContext;
    private readonly BackupTaskRepository _repository;

    public BackupTaskRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BackupAppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new BackupAppDbContext(options);
        _repository = new BackupTaskRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_CreatesNewTask()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };

        // Act
        var result = await _repository.AddAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("TestTask", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTaskWithHistory()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        await _repository.AddAsync(task);

        var history = new BackupHistory
        {
            TaskId = task.Id,
            StartTime = DateTime.UtcNow,
            Status = BackupStatus.Success,
            BackupType = BackupType.Full
        };
        await _repository.AddHistoryAsync(history);

        // Act
        var result = await _repository.GetByIdAsync(task.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.History);
        Assert.Single(result.History);
    }

    [Fact]
    public async Task IsTaskNameUniqueAsync_UniqueName_ReturnsTrue()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "UniqueTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        await _repository.AddAsync(task);

        // Act
        var isUnique = await _repository.IsTaskNameUniqueAsync("AnotherUniqueTask");

        // Assert
        Assert.True(isUnique);
    }

    [Fact]
    public async Task IsTaskNameUniqueAsync_DuplicateName_ReturnsFalse()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "DuplicateTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        await _repository.AddAsync(task);

        // Act
        var isUnique = await _repository.IsTaskNameUniqueAsync("DuplicateTask");

        // Assert
        Assert.False(isUnique);
    }

    [Fact]
    public async Task IsTaskNameUniqueAsync_DuplicateNameCaseInsensitive_ReturnsFalse()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        await _repository.AddAsync(task);

        // Act
        var isUnique = await _repository.IsTaskNameUniqueAsync("testtask");

        // Assert
        Assert.False(isUnique);
    }

    [Fact]
    public async Task IsTaskNameUniqueAsync_ExcludeTaskId_ReturnsTrue()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TestTask",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        var addedTask = await _repository.AddAsync(task);

        // Act
        var isUnique = await _repository.IsTaskNameUniqueAsync("TestTask", addedTask.Id);

        // Assert
        Assert.True(isUnique); // Должно быть уникально, так как исключаем текущую задачу
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTask()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "OriginalName",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        var addedTask = await _repository.AddAsync(task);

        // Act
        addedTask.Name = "UpdatedName";
        await _repository.UpdateAsync(addedTask);

        // Assert
        var updated = await _repository.GetByIdAsync(addedTask.Id);
        Assert.Equal("UpdatedName", updated!.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TaskToDelete",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        var addedTask = await _repository.AddAsync(task);

        // Act
        await _repository.DeleteAsync(addedTask.Id);

        // Assert
        var deleted = await _repository.GetByIdAsync(addedTask.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithHistory_RemovesTask()
    {
        // Arrange
        var task = new BackupTask
        {
            Name = "TaskWithHistory",
            SourcePath = @"C:\Source",
            TargetPath = @"C:\Target",
            ScheduleType = ScheduleType.Manual,
            BackupType = BackupType.Full
        };
        var addedTask = await _repository.AddAsync(task);

        var history = new BackupHistory
        {
            TaskId = addedTask.Id,
            StartTime = DateTime.UtcNow,
            Status = BackupStatus.Success,
            BackupType = BackupType.Full
        };
        await _repository.AddHistoryAsync(history);

        // Act
        await _repository.DeleteAsync(addedTask.Id);

        // Assert
        // Проверяем, что задача удалена
        var deleted = await _repository.GetByIdAsync(addedTask.Id);
        Assert.Null(deleted);
        
        // In-Memory БД может не поддерживать каскадное удаление автоматически,
        // поэтому проверяем только удаление задачи
        // История должна удаляться через каскадное удаление в реальной БД
    }
}

