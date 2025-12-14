namespace BackupApp.WPF.Services;

public enum MessageType
{
    Information,
    Warning,
    Error
}

public interface IMessageService
{
    void ShowMessage(string message, string title, MessageType type = MessageType.Information);
    bool ShowConfirmation(string message, string title);
    
    /// <summary>
    /// Запрашивает путь к файлу у пользователя
    /// </summary>
    /// <param name="title">Заголовок диалога</param>
    /// <param name="filter">Фильтр типов файлов</param>
    /// <returns>Путь к выбранному файлу или null, если выбор отменен</returns>
    string? RequestFilePath(string title, string filter);
    
    /// <summary>
    /// Запрашивает путь к папке у пользователя
    /// </summary>
    /// <param name="title">Заголовок диалога</param>
    /// <param name="initialDirectory">Начальная директория</param>
    /// <returns>Путь к выбранной папке или null, если выбор отменен</returns>
    string? RequestFolderPath(string title, string initialDirectory);
}