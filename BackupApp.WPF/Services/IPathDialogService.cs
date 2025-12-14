namespace BackupApp.WPF.Services;

public interface IPathDialogService
{
    string? BrowseFolder(string? initialPath = null, string? description = null);
}

























