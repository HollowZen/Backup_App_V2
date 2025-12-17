using System;
using BackupApp.Core.Constants;
using BackupApp.Core.Models;
using BackupApp.WPF.ViewModels;
using Moq;
using Xunit;

namespace BackupApp.Tests.ViewModels;

public class TaskViewModelValidationTests
{
    private readonly Mock<BackupApp.WPF.Services.IPathDialogService> _pathDialogServiceMock;
    private readonly Mock<BackupApp.Core.Services.IEncryptionService> _encryptionServiceMock;

    public TaskViewModelValidationTests()
    {
        _pathDialogServiceMock = new Mock<BackupApp.WPF.Services.IPathDialogService>();
        _encryptionServiceMock = new Mock<BackupApp.Core.Services.IEncryptionService>();
    }

    private TaskViewModel CreateViewModel()
    {
        return new TaskViewModel(_pathDialogServiceMock.Object, _encryptionServiceMock.Object);
    }

    [Fact]
    public void Validate_EmptyName_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = string.Empty;
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("Имя задачи", vm.ValidationError);
    }

    [Fact]
    public void Validate_EmptySourcePath_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = string.Empty;
        vm.TargetPath = @"C:\Target";

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("путь источника", vm.ValidationError);
    }

    [Fact]
    public void Validate_EmptyTargetPath_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = string.Empty;

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("путь назначения", vm.ValidationError);
    }

    [Fact]
    public void Validate_ValidTask_ReturnsTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";

        // Act
        var result = vm.Validate();

        // Assert
        Assert.True(result);
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void Validate_WeeklyScheduleWithoutDays_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.ScheduleType = ScheduleType.Weekly;
        vm.ScheduleTimeValue = TimeSpan.FromHours(10);

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("день недели", vm.ValidationError);
    }

    [Fact]
    public void Validate_MonthlyScheduleWithoutDay_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.ScheduleType = ScheduleType.Monthly;
        vm.ScheduleTimeValue = TimeSpan.FromHours(10);
        vm.ScheduleDayOfMonth = null;

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("День месяца", vm.ValidationError);
    }

    [Fact]
    public void Validate_EncryptionWithoutPassword_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.UseEncryption = true;
        vm.EncryptionPassword = string.Empty;

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains("пароль", vm.ValidationError);
    }

    [Fact]
    public void Validate_EncryptionWithShortPassword_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.UseEncryption = true;
        vm.EncryptionPassword = "short"; // Меньше 8 символов

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains($"{AppConstants.MinPasswordLength} символов", vm.ValidationError);
    }

    [Fact]
    public void Validate_EncryptionWithLongPassword_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.UseEncryption = true;
        vm.EncryptionPassword = new string('a', AppConstants.MaxPasswordLength + 1); // Больше максимума

        // Act
        var result = vm.Validate();

        // Assert
        Assert.False(result);
        Assert.Contains($"{AppConstants.MaxPasswordLength} символов", vm.ValidationError);
    }

    [Fact]
    public void Validate_EncryptionWithValidPassword_ReturnsTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.UseEncryption = true;
        vm.EncryptionPassword = "ValidPassword123"; // 16 символов
        vm.UseCompression = false; // Явно отключаем сжатие

        // Act
        var result = vm.Validate();

        // Assert
        Assert.True(result);
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void Validate_CompressionLevelOutOfRange_AutomaticallyClamped()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Name = "TestTask";
        vm.SourcePath = @"C:\Source";
        vm.TargetPath = @"C:\Target";
        vm.UseCompression = true;
        vm.UseEncryption = false;

        // Act - устанавливаем значение вне диапазона
        vm.CompressionLevel = 15; // Вне диапазона 0-9, но автоматически ограничивается через Math.Clamp

        // Assert - значение должно быть автоматически ограничено
        Assert.Equal(AppConstants.MaxCompressionLevel, vm.CompressionLevel); // Должно стать 9
        
        // Валидация должна пройти, так как значение уже ограничено
        var result = vm.Validate();
        Assert.True(result); // Валидация проходит, так как CompressionLevel автоматически ограничен
    }
}

