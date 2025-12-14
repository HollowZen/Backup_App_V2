using BackupApp.Core.Services;
using BackupApp.WPF.Commands;
using BackupApp.WPF.Helpers;
using BackupApp.WPF.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace BackupApp.WPF.ViewModels;

public class DecryptWindowViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly IMessageService _messageService;
    private readonly IPathDialogService _pathDialogService;

    private string? _encryptedFilePath;
    private string? _outputDirectory;
    private string? _password;
    private string? _statusMessage;
    private bool _isDecrypting;
    private bool _isDecryptionSuccessful;

    public DecryptWindowViewModel(
        IBackupService backupService,
        IMessageService messageService,
        IPathDialogService pathDialogService)
    {
        _backupService = backupService;
        _messageService = messageService;
        _pathDialogService = pathDialogService;

        BrowseEncryptedFileCommand = new RelayCommand(_ => BrowseEncryptedFile());
        BrowseOutputDirectoryCommand = new RelayCommand(_ => BrowseOutputDirectory());
        DecryptCommand = new RelayCommand(_ => DecryptAsync(), _ => !IsDecrypting);
    }

    public RelayCommand BrowseEncryptedFileCommand { get; }
    public RelayCommand BrowseOutputDirectoryCommand { get; }
    public RelayCommand DecryptCommand { get; }

    public string? EncryptedFilePath
    {
        get => _encryptedFilePath;
        set
        {
            if (SetProperty(ref _encryptedFilePath, value))
            {
                // Auto-populate output directory based on encrypted file path
                if (string.IsNullOrWhiteSpace(OutputDirectory) && !string.IsNullOrWhiteSpace(value))
                {
                    var defaultOutputDir = Path.Combine(Path.GetDirectoryName(value)!, "Decrypted");
                    OutputDirectory = defaultOutputDir;
                }
                DecryptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                DecryptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                DecryptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsDecrypting
    {
        get => _isDecrypting;
        private set
        {
            if (SetProperty(ref _isDecrypting, value))
            {
                DecryptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsDecryptionSuccessful
    {
        get => _isDecryptionSuccessful;
        private set => SetProperty(ref _isDecryptionSuccessful, value);
    }

    private void BrowseEncryptedFile()
    {
        var filePath = _messageService.RequestFilePath(
            "Выберите зашифрованный файл резервной копии",
            "Зашифрованные файлы (*.enc)|*.enc|ZIP-архивы (*.zip)|*.zip|Все файлы (*.*)|*.*");

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            EncryptedFilePath = filePath;
        }
    }

    private void BrowseOutputDirectory()
    {
        var folderPath = _pathDialogService.BrowseFolder(
            OutputDirectory,
            "Выберите папку для расшифрованных файлов");

        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            OutputDirectory = folderPath;
        }
    }

    private async Task DecryptAsync()
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(EncryptedFilePath))
        {
            _messageService.ShowMessage(
                "Пожалуйста, выберите зашифрованный файл.",
                "Ошибка",
                MessageType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            _messageService.ShowMessage(
                "Пожалуйста, выберите папку для расшифрованных файлов.",
                "Ошибка",
                MessageType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            _messageService.ShowMessage(
                "Пожалуйста, введите пароль для расшифровки.",
                "Ошибка",
                MessageType.Warning);
            return;
        }

        IsDecrypting = true;
        StatusMessage = "Расшифровка файла...";

        try
        {
            var result = await _backupService.DecryptBackupAsync(
                EncryptedFilePath,
                OutputDirectory,
                Password,
                default);

            IsDecryptionSuccessful = true;
            StatusMessage = $"Расшифровка успешно завершена! Расшифровано файлов: {result.FilesCopied}";

            // Уведомляем о завершении (переключение вкладки обрабатывается в MainWindowViewModel)
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex);
            StatusMessage = $"Ошибка расшифровки: {userMessage}";
            _messageService.ShowMessage(
                $"Ошибка расшифровки: {userMessage}",
                "Ошибка",
                MessageType.Error);
            IsDecryptionSuccessful = false;
        }
        finally
        {
            IsDecrypting = false;
        }
    }

    public event Action? CloseRequested;
}

