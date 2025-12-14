using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using Ookii.Dialogs.Wpf;
using System.Windows;
using Microsoft.Win32;

namespace BackupApp.WPF.Services;

public class MessageService : IMessageService
{

    public void ShowMessage(string message, string title, MessageType type = MessageType.Information)
    {
        MessageBoxImage image = type switch
        {
            MessageType.Warning => MessageBoxImage.Warning,
            MessageType.Error => MessageBoxImage.Error,
            _ => MessageBoxImage.Information
        };

        var owner = WpfApplication.Current?.MainWindow;
        if (owner is not null)
        {
            WpfMessageBox.Show(owner, message, title, MessageBoxButton.OK, image);
        }
        else
        {
            WpfMessageBox.Show(message, title, MessageBoxButton.OK, image);
        }
    }

    public bool ShowConfirmation(string message, string title)
    {
        var owner = WpfApplication.Current?.MainWindow;
        var result = owner is not null
            ? WpfMessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : WpfMessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
    
    public string? RequestFilePath(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };
        
        var owner = WpfApplication.Current?.MainWindow;
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    
    public string? RequestFolderPath(string title, string initialDirectory)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            SelectedPath = initialDirectory
        };
        
        var owner = WpfApplication.Current?.MainWindow;
        return dialog.ShowDialog(owner) == true ? dialog.SelectedPath : null;
    }
    
}