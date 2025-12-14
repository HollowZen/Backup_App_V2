using System;

namespace BackupApp.WPF.Helpers;

/// <summary>
/// Вспомогательный класс для форматирования данных
/// </summary>
public static class FormatHelper
{
    private static readonly string[] SizeUnits = { "КБ", "МБ", "ГБ", "ТБ" };
    private const int BytesPerKilobyte = 1024;

    /// <summary>
    /// Форматирует размер в байтах в читаемый формат
    /// </summary>
    public static string FormatSize(double bytes)
    {
        if (bytes < BytesPerKilobyte) 
            return $"{bytes:0.#} Б";

        int order = 0;
        double len = bytes / BytesPerKilobyte;

        while (len >= BytesPerKilobyte && order < SizeUnits.Length - 1)
        {
            order++;
            len /= BytesPerKilobyte;
        }

        return $"{len:0.##} {SizeUnits[order]}";
    }

    /// <summary>
    /// Форматирует длительность в читаемый формат
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1) 
            return duration.ToString(@"hh\:mm\:ss");
        
        if (duration.TotalSeconds < 1) 
            return "<1 c";
        
        return duration.ToString(@"mm\:ss");
    }
}




