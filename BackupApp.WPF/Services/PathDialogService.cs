using System.Windows;
using Ookii.Dialogs.Wpf;

namespace BackupApp.WPF.Services;

public class PathDialogService : IPathDialogService
{
    public string? BrowseFolder(string? initialPath = null, string? description = null)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            SelectedPath = initialPath ?? string.Empty,
            Description = description ?? "Выберите папку",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        var owner = Application.Current?.MainWindow;
        var result = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        return result == true ? dialog.SelectedPath : null;
    }
}

























