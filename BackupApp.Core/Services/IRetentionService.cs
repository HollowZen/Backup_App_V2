using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

/// <summary>
/// Интерфейс сервиса управления политиками хранения бэкапов
/// </summary>
public interface IRetentionService
{
    /// <summary>
    /// Применяет политику хранения для указанной задачи
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ApplyRetentionPolicyAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подсчитывает количество версий для задачи
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество версий</returns>
    Task<int> CountVersionsAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Определяет, какие версии бэкапов следует удалить согласно политике
    /// </summary>
    /// <param name="histories">История бэкапов</param>
    /// <param name="policy">Тип политики хранения</param>
    /// <param name="maxVersions">Максимальное количество версий</param>
    /// <param name="maxAgeDays">Максимальный возраст в днях</param>
    /// <returns>Коллекция версий для удаления</returns>
    ICollection<BackupHistory> GetVersionsToDelete(IReadOnlyList<BackupHistory> histories, RetentionPolicyType policy, int? maxVersions, int? maxAgeDays);
}
