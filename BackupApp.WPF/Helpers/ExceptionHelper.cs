using System;

namespace BackupApp.WPF.Helpers;

/// <summary>
/// Вспомогательный класс для работы с исключениями
/// </summary>
public static class ExceptionHelper
{
    /// <summary>
    /// Получает понятное сообщение об ошибке для пользователя
    /// </summary>
    public static string GetUserFriendlyMessage(Exception ex)
    {
        return ex switch
        {
            ArgumentException argEx => $"Неверный параметр: {argEx.Message}",
            InvalidOperationException opEx => $"Операция не может быть выполнена: {opEx.Message}",
            UnauthorizedAccessException => "Недостаточно прав для выполнения операции.",
            System.IO.DirectoryNotFoundException => "Папка не найдена.",
            System.IO.FileNotFoundException => "Файл не найден.",
            System.IO.IOException ioEx => $"Ошибка ввода-вывода: {ioEx.Message}",
            _ => $"Произошла ошибка: {ex.Message}"
        };
    }
}




