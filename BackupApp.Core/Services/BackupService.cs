using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Core.Constants;
using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public class BackupService : IBackupService
{
    private const int BufferSize = 1024 * 128;
    private const long SmallFileThresholdBytes = 512 * 1024;
    private const int MaxPathLength = 260; // Windows MAX_PATH
    private static readonly int ParallelSmallFileDegree = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

    private readonly IEncryptionService _encryptionService;

    public BackupService(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task<BackupExecutionResult> ExecuteBackupAsync(BackupTask task, IProgress<BackupProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task.SourcePath))
        {
            throw new DirectoryNotFoundException("Исходная папка не указана.");
        }

        if (!Directory.Exists(task.SourcePath))
        {
            throw new DirectoryNotFoundException($"Исходная папка не найдена: '{task.SourcePath}'");
        }

        if (string.IsNullOrWhiteSpace(task.TargetPath))
        {
            throw new DirectoryNotFoundException("Целевая папка не указана.");
        }

        // Проверяем доступность диска и свободное место
        CheckDiskAvailability(task.TargetPath);

        Directory.CreateDirectory(task.TargetPath);

        var sourceDir = new DirectoryInfo(task.SourcePath);
        var targetDir = new DirectoryInfo(task.TargetPath);

        var selection = SelectFiles(task, sourceDir);
        
        // Если нет файлов для копирования, не создаем папку и артефакт
        if (selection.Files.Count == 0)
        {
            var emptyResult = new BackupExecutionResult(task.SourcePath, task.TargetPath)
            {
                TotalBytes = 0,
                FilesSkipped = selection.SkippedFiles,
                PerformedBackupType = selection.EffectiveType,
                UsedCompression = task.UseCompression,
                CompressionLevel = task.UseCompression ? Math.Clamp(task.CompressionLevel, AppConstants.MinCompressionLevel, AppConstants.MaxCompressionLevel) : 0,
                FilesCopied = 0,
                BytesWritten = 0,
                OutputArtifactPath = null
            };
            emptyResult.StartedAt = DateTime.UtcNow;
            emptyResult.CompletedAt = DateTime.UtcNow;
            emptyResult.Duration = TimeSpan.Zero;
            return emptyResult;
        }
        
        // Проверяем свободное место на диске (с запасом 10% для безопасности)
        var requiredSpace = selection.TotalBytes;
        if (task.UseCompression)
        {
            // При сжатии требуется меньше места, но берем 50% от исходного размера как оценку
            requiredSpace = (long)(requiredSpace * 0.5);
        }
        CheckDiskSpace(task.TargetPath, requiredSpace);
        
        var result = new BackupExecutionResult(task.SourcePath, task.TargetPath)
        {
            TotalBytes = selection.TotalBytes,
            FilesSkipped = selection.SkippedFiles,
            PerformedBackupType = selection.EffectiveType,
            UsedCompression = task.UseCompression,
            CompressionLevel = task.UseCompression ? Math.Clamp(task.CompressionLevel, AppConstants.MinCompressionLevel, AppConstants.MaxCompressionLevel) : 0
        };

        var context = new TransferContext(selection.TotalBytes, progress, result);

        string? artifactPath = null;
        var compressionLevel = MapCompressionLevel(task.CompressionLevel);
        var mustArchive = task.UseCompression || task.UseEncryption;

        // Для каждого бэкапа создаём отдельную вложенную папку внутри целевого каталога
        var backupFolderName = BuildBackupFolderName(task, selection.EffectiveType);
        var backupRootPath = Path.Combine(targetDir.FullName, backupFolderName);
        Directory.CreateDirectory(backupRootPath);
        // Считаем эту папку "артефактом" бэкапа (внутри неё могут быть файлы/архивы)
        result.OutputArtifactPath = backupRootPath;

        if (mustArchive)
        {
            var archivePath = BuildArchivePath(task, new DirectoryInfo(backupRootPath));
            artifactPath = archivePath;
            await CreateArchiveAsync(selection.Files, sourceDir, archivePath, compressionLevel, context, cancellationToken);
            if (File.Exists(archivePath))
            {
                result.BytesWritten = new FileInfo(archivePath).Length;
            }
        }
        else
        {
            var backupRootDir = new DirectoryInfo(backupRootPath);
            await CopyToDirectoryAsync(selection.Files, sourceDir, backupRootDir, context, cancellationToken);
            result.BytesWritten = result.TotalBytes;
        }

        if (task.UseEncryption)
        {
            var password = ExtractEncryptionPassword(task.EncryptionPasswordHash);
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Пароль шифрования не найден или повреждён.");
            }

            if (artifactPath is null)
            {
                // если по какой-то причине ещё нет архива — создаём его внутри папки бэкапа
                artifactPath = BuildArchivePath(task, new DirectoryInfo(backupRootPath));
                await CreateArchiveAsync(selection.Files, sourceDir, artifactPath, compressionLevel, context, cancellationToken);
            }

            // шифруем архив в той же вложенной папке
            var encryptedFileName = Path.GetFileName(artifactPath) + ".enc";
            var encryptedPath = Path.Combine(backupRootPath, encryptedFileName);
            await _encryptionService.EncryptFileAsync(artifactPath, encryptedPath, password, cancellationToken);
            TryDeleteFileSafe(artifactPath);
            // OutputArtifactPath уже указывает на папку бэкапа (backupRootPath)
        }

        context.Complete();
        result.Duration = context.Stopwatch.Elapsed;
        result.CompletedAt = DateTime.UtcNow;

        // Если ничего не скопировалось, удаляем созданную папку и очищаем артефакт
        if (result.FilesCopied == 0 && result.OutputArtifactPath != null)
        {
            try
            {
                if (Directory.Exists(result.OutputArtifactPath))
                {
                    Directory.Delete(result.OutputArtifactPath, recursive: true);
                }
                result.OutputArtifactPath = null;
            }
            catch
            {
                // Игнорируем ошибки при удалении - папка может быть занята или уже удалена
            }
        }

        return result;
    }

    /// <summary>
    /// Расшифровывает зашифрованный архив резервной копии
    /// </summary>
    /// <param name="encryptedFilePath">Путь к зашифрованному файлу</param>
    /// <param name="outputDirectory">Директория для расшифрованных файлов</param>
    /// <param name="password">Пароль для расшифровки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения расшифровки</returns>
    public async Task<BackupExecutionResult> DecryptBackupAsync(string encryptedFilePath, string outputDirectory, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedFilePath))
        {
            throw new ArgumentException("Путь к зашифрованному файлу не указан.", nameof(encryptedFilePath));
        }

        if (!File.Exists(encryptedFilePath))
        {
            throw new FileNotFoundException($"Зашифрованный файл не найден: '{encryptedFilePath}'");
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Папка для расшифрованных файлов не указана.", nameof(outputDirectory));
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            throw new DirectoryNotFoundException($"Не удалось создать папку для расшифрованных файлов: '{outputDirectory}'. {ex.Message}", ex);
        }

        var result = new BackupExecutionResult(encryptedFilePath, outputDirectory)
        {
            PerformedBackupType = BackupType.Full,
            UsedCompression = true
        };

        var fileInfo = new FileInfo(encryptedFilePath);
        result.TotalBytes = fileInfo.Length;

        // путь для расшифрованного файла, временный
        var decryptedFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(encryptedFilePath));

        // если файл зашифрован но не архив, просто расшифровываем
        if (Path.GetExtension(encryptedFilePath).Equals(".enc", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetFileNameWithoutExtension(encryptedFilePath).EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // это просто зашифрованный файл без архива
            await _encryptionService.DecryptFileAsync(encryptedFilePath, decryptedFilePath, password, cancellationToken);

            result.BytesWritten = new FileInfo(decryptedFilePath).Length;
            result.FilesCopied = 1;
        }
        else if (Path.GetExtension(encryptedFilePath).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            // это зашифрованный архив, сначала расшифровываем потом распаковываем
            var zipFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(encryptedFilePath));
            await _encryptionService.DecryptFileAsync(encryptedFilePath, zipFilePath, password, cancellationToken);

            // распаковываем архив
            await ExtractArchiveAsync(zipFilePath, outputDirectory, result, cancellationToken);

            // удаляем временный расшифрованный архив, он больше не нужен
            TryDeleteFileSafe(zipFilePath);
        }
        else if (Path.GetExtension(encryptedFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // это просто архив без шифрования, сразу распаковываем
            await ExtractArchiveAsync(encryptedFilePath, outputDirectory, result, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Unsupported file format for decryption.");
        }

        result.Duration = TimeSpan.Zero; // длительность пока не считаем, можно добавить потом
        result.CompletedAt = DateTime.UtcNow;

        // Если ничего не скопировалось, удаляем созданную папку и очищаем артефакт
        if (result.FilesCopied == 0 && result.OutputArtifactPath != null)
        {
            try
            {
                if (Directory.Exists(result.OutputArtifactPath))
                {
                    Directory.Delete(result.OutputArtifactPath, recursive: true);
                }
                result.OutputArtifactPath = null;
            }
            catch
            {
                // Игнорируем ошибки при удалении - папка может быть занята или уже удалена
            }
        }

        return result;
    }

    public Task<long> CalculateDirectorySizeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult(0L);
        }

        long totalSize = 0;
        var dir = new DirectoryInfo(path);

        foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalSize += file.Length;
        }

        return Task.FromResult(totalSize);
    }

    private static FileSelectionResult SelectFiles(BackupTask task, DirectoryInfo sourceRoot)
    {
        var allFiles = sourceRoot.EnumerateFiles("*", SearchOption.AllDirectories).ToList();

        var baseline = task.BackupType switch
        {
            BackupType.Full => null,
            BackupType.Incremental => task.LastBackupTime,
            BackupType.Differential => task.LastFullBackupTime,
            _ => null
        };

        var effectiveType = baseline.HasValue ? task.BackupType : BackupType.Full;

        var filesToCopy = baseline.HasValue
            // Используем > вместо >=, чтобы копировать только файлы, измененные ПОСЛЕ baseline
            // Добавляем 1 секунду для учета точности файловой системы Windows (округляет до 2 секунд)
            ? allFiles.Where(f => f.LastWriteTimeUtc > baseline.Value.AddSeconds(1)).ToList()
            : allFiles;

        var totalBytes = filesToCopy.Sum(f => f.Length);
        var skipped = allFiles.Count - filesToCopy.Count;

        return new FileSelectionResult(filesToCopy, effectiveType, skipped, totalBytes);
    }

    private static string BuildArchivePath(BackupTask task, DirectoryInfo targetDir)
    {
        var safeName = string.Concat(task.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{(string.IsNullOrWhiteSpace(safeName) ? "backup" : safeName)}_{timestamp}_{task.BackupType}.zip";
        return Path.Combine(targetDir.FullName, fileName);
    }

    private static string BuildBackupFolderName(BackupTask task, BackupType effectiveType)
    {
        var safeName = string.Concat(task.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"{(string.IsNullOrWhiteSpace(safeName) ? "backup" : safeName)}_{timestamp}_{effectiveType}";
    }

    private static CompressionLevel MapCompressionLevel(int level) => level switch
    {
        <= 0 => CompressionLevel.NoCompression,
        <= 3 => CompressionLevel.Fastest,
        >= 8 => CompressionLevel.SmallestSize,
        _ => CompressionLevel.Optimal
    };

    private string? ExtractEncryptionPassword(string? protectedPayload)
    {
        if (string.IsNullOrWhiteSpace(protectedPayload))
        {
            return null;
        }

        var parts = protectedPayload.Split('|', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(parts[1]);
            var entropy = Encoding.UTF8.GetBytes(AppConstants.EncryptionEntropy);
            var decrypted = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            var password = Encoding.UTF8.GetString(decrypted);
            return _encryptionService.VerifyPassword(password, parts[0]) ? password : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteFileSafe(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // если не удалось удалить файл - не критично, просто игнорируем ошибку
        }
    }

    private static async Task CopyToDirectoryAsync(
        IReadOnlyList<FileInfo> files,
        DirectoryInfo sourceRoot,
        DirectoryInfo targetRoot,
        TransferContext context,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var smallFiles = files.Where(f => f.Length <= SmallFileThresholdBytes).ToList();
        var largeFiles = files.Where(f => f.Length > SmallFileThresholdBytes).ToList();

        if (smallFiles.Count > 0)
        {
            await Parallel.ForEachAsync(
                smallFiles,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = ParallelSmallFileDegree
                },
                async (file, token) => await CopySingleFileAsync(file, sourceRoot, targetRoot, context, token));
        }

        foreach (var file in largeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopySingleFileAsync(file, sourceRoot, targetRoot, context, cancellationToken);
        }
    }

    private static async Task CopySingleFileAsync(
        FileInfo file,
        DirectoryInfo sourceRoot,
        DirectoryInfo targetRoot,
        TransferContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var relativePath = Path.GetRelativePath(sourceRoot.FullName, file.FullName);
            var destinationPath = Path.Combine(targetRoot.FullName, relativePath);
            
            // Нормализуем путь безопасным способом
            try
            {
                destinationPath = Path.GetFullPath(destinationPath);
            }
            catch
            {
                // Если GetFullPath не работает, используем исходный путь
                // Это может произойти, если путь еще не существует
            }
            
            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            // Убеждаемся, что директория создана перед созданием файла
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                // Нормализуем путь директории безопасным способом
                string normalizedDir;
                try
                {
                    normalizedDir = Path.GetFullPath(destinationDirectory);
                }
                catch
                {
                    normalizedDir = destinationDirectory;
                }
                
                context.EnsureDirectory(normalizedDir);
                
                // Дополнительная проверка - убеждаемся, что директория действительно существует
                // Ждем до 1 секунды, если директория еще создается
                int attempts = 0;
                while (!Directory.Exists(normalizedDir) && attempts < 100)
                {
                    await Task.Delay(10, cancellationToken);
                    attempts++;
                }
                
                if (!Directory.Exists(normalizedDir))
                {
                    throw new DirectoryNotFoundException($"Директория не была создана после ожидания: '{normalizedDir}'");
                }
            }

            await using var sourceStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var targetStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

            await CopyWithProgressAsync(sourceStream, targetStream, context, relativePath, cancellationToken);
            PreserveMetadata(file, destinationPath);
            context.CompleteFile(file.Length);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new DirectoryNotFoundException($"Не удалось скопировать файл '{file.FullName}'. Возможно, путь слишком длинный или содержит недопустимые символы. Ошибка: {ex.Message}", ex);
        }
        catch (PathTooLongException ex)
        {
            throw new PathTooLongException($"Путь к файлу слишком длинный: '{file.FullName}'. Windows имеет ограничение на длину пути (260 символов).", ex);
        }
    }

    private static async Task CreateArchiveAsync(
        IReadOnlyList<FileInfo> files,
        DirectoryInfo sourceRoot,
        string archivePath,
        CompressionLevel level,
        TransferContext context,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceRoot.FullName, file.FullName).Replace('\\', '/');
            var entry = archive.CreateEntry(relative, level);
            entry.LastWriteTime = file.LastWriteTime;

            await using var entryStream = entry.Open();
            await using var sourceStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            await CopyWithProgressAsync(sourceStream, entryStream, context, relative, cancellationToken);
            context.CompleteFile(file.Length);
        }
    }

    /// <summary>
    /// Распаковывает архив в указанную директорию
    /// </summary>
    private static async Task ExtractArchiveAsync(string archivePath, string outputDirectory, BackupExecutionResult result, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
                continue; // Пропускаем директории

            var destinationPath = Path.Combine(outputDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await using var entryStream = entry.Open();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            await entryStream.CopyToAsync(fileStream, cancellationToken);

            result.FilesCopied++;
        }

        result.DirectoriesCreated = Directory.GetDirectories(outputDirectory, "*", SearchOption.AllDirectories).Length;
        result.BytesWritten = new DirectoryInfo(outputDirectory).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        TransferContext context,
        string currentFile,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                context.Report(bytesRead, currentFile);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void PreserveMetadata(FileInfo sourceFile, string destinationPath)
    {
        File.SetCreationTimeUtc(destinationPath, sourceFile.CreationTimeUtc);
        File.SetLastWriteTimeUtc(destinationPath, sourceFile.LastWriteTimeUtc);
        File.SetAttributes(destinationPath, sourceFile.Attributes);
    }

    private static void CheckDiskAvailability(string path)
    {
        try
        {
            var rootPath = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new IOException($"Не удалось определить корневой путь для: '{path}'");
            }

            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady)
            {
                throw new IOException($"Диск '{rootPath}' недоступен или не готов.");
            }
        }
        catch (Exception ex) when (ex is not IOException)
        {
            throw new IOException($"Ошибка при проверке доступности диска для пути '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Обеспечивает поддержку длинных путей (>260 символов) через префикс \\?\
    /// </summary>
    private static string EnsureLongPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Если путь уже имеет префикс длинного пути, возвращаем как есть
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return path;
        }

        // Если путь длиннее MAX_PATH, добавляем префикс
        if (path.Length > MaxPathLength)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                // UNC путь
                return @"\\?\UNC\" + path.Substring(2);
            }
            else
            {
                // Локальный путь
                return @"\\?\" + path;
            }
        }

        return path;
    }

    private static void CheckDiskSpace(string path, long requiredBytes)
    {
        try
        {
            var rootPath = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return; // Не можем проверить, пропускаем
            }

            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady)
            {
                return; // Диск не готов, пропускаем проверку
            }

            // Добавляем 10% запаса для безопасности
            var requiredWithMargin = (long)(requiredBytes * 1.1);
            
            if (drive.AvailableFreeSpace < requiredWithMargin)
            {
                var requiredMB = requiredWithMargin / (1024.0 * 1024.0);
                var availableMB = drive.AvailableFreeSpace / (1024.0 * 1024.0);
                throw new IOException($"Недостаточно места на диске '{rootPath}'. Требуется: {requiredMB:F2} МБ, доступно: {availableMB:F2} МБ");
            }
        }
        catch (IOException)
        {
            throw; // Пробрасываем IOException как есть
        }
        catch (Exception ex)
        {
            // Для других ошибок просто логируем, но не останавливаем процесс
            // Это может быть проблема с сетевыми дисками или другими особыми случаями
            System.Diagnostics.Debug.WriteLine($"Не удалось проверить свободное место на диске: {ex.Message}");
        }
    }

    private sealed record FileSelectionResult(
        IReadOnlyList<FileInfo> Files,
        BackupType EffectiveType,
        int SkippedFiles,
        long TotalBytes);

    private sealed class TransferContext
    {
        private readonly IProgress<BackupProgressReport>? _progress;
        private readonly BackupExecutionResult _result;
        private readonly ConcurrentDictionary<string, byte> _createdDirectories = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _directoryLock = new object();
        private long _processedBytes;
        private int _completedFiles;
        private int _createdDirsCount;

        public TransferContext(long totalBytes, IProgress<BackupProgressReport>? progress, BackupExecutionResult result)
        {
            TotalBytes = totalBytes;
            _progress = progress;
            _result = result;
            Stopwatch = Stopwatch.StartNew();
            if (totalBytes == 0)
            {
                _progress?.Report(new BackupProgressReport(0, 0, 0, string.Empty, 0));
            }
        }

        public long TotalBytes { get; }

        public Stopwatch Stopwatch { get; }

        public void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Нормализуем путь безопасным способом
            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path);
            }
            catch
            {
                // Если GetFullPath не работает (например, путь еще не существует), используем исходный путь
                normalizedPath = path;
            }

            // Используем блокировку для синхронизации между потоками
            lock (_directoryLock)
            {
                // Проверяем, не создается ли уже эта директория другим потоком
                if (_createdDirectories.ContainsKey(normalizedPath))
                {
                    // Ждем, пока директория будет создана
                    while (!Directory.Exists(normalizedPath))
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    return;
                }

                // Помечаем директорию как создаваемую
                _createdDirectories.TryAdd(normalizedPath, 0);

                try
                {
                    // Проверяем, существует ли директория
                    if (!Directory.Exists(normalizedPath))
                    {
                        // Создаем директорию рекурсивно - это гарантирует создание всех промежуточных директорий
                        Directory.CreateDirectory(normalizedPath);
                        
                        // Убеждаемся, что директория действительно создана
                        if (!Directory.Exists(normalizedPath))
                        {
                            throw new DirectoryNotFoundException($"Директория не была создана: '{normalizedPath}'");
                        }
                        
                        Interlocked.Increment(ref _createdDirsCount);
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // Если директория не найдена, пытаемся создать родительские директории рекурсивно
                    CreateDirectoryRecursive(normalizedPath);
                }
                catch (Exception ex)
                {
                    // Для других ошибок пробуем создать родительские директории рекурсивно
                    try
                    {
                        CreateDirectoryRecursive(normalizedPath);
                    }
                    catch
                    {
                        _createdDirectories.TryRemove(normalizedPath, out _);
                        throw new DirectoryNotFoundException($"Не удалось создать директорию: '{normalizedPath}'. Ошибка: {ex.Message}", ex);
                    }
                }
            }
        }

        private void CreateDirectoryRecursive(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Проверяем, существует ли уже директория
            if (Directory.Exists(path))
            {
                return;
            }

            var parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parentDir) && parentDir != path)
            {
                // Рекурсивно создаем родительские директории
                if (!_createdDirectories.ContainsKey(parentDir))
                {
                    EnsureDirectory(parentDir);
                }
                else
                {
                    // Если родительская директория уже создается, ждем
                    while (!Directory.Exists(parentDir))
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    CreateDirectoryRecursive(parentDir);
                }
            }

            // Создаем текущую директорию
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                
                // Убеждаемся, что директория действительно создана
                if (!Directory.Exists(path))
                {
                    throw new DirectoryNotFoundException($"Директория не была создана: '{path}'");
                }
                
                Interlocked.Increment(ref _createdDirsCount);
            }
        }

        public void Report(int bytes, string file)
        {
            if (bytes <= 0)
            {
                return;
            }

            var processed = Interlocked.Add(ref _processedBytes, bytes);
            var files = Volatile.Read(ref _completedFiles);
            var elapsed = Math.Max(Stopwatch.Elapsed.TotalSeconds, 0.1);
            var speed = processed / elapsed;
            _progress?.Report(new BackupProgressReport(TotalBytes, processed, files, file, speed));
        }

        public void CompleteFile(long _)
        {
            Interlocked.Increment(ref _completedFiles);
        }

        public void Complete()
        {
            Stopwatch.Stop();
            _result.FilesCopied = _completedFiles;
            _result.DirectoriesCreated = _createdDirsCount;
            _progress?.Report(new BackupProgressReport(TotalBytes, TotalBytes, _completedFiles, string.Empty, 0));
        }
    }
}