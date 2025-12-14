using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BackupApp.Data;

/// <summary>
/// Фабрика контекста для инструментов EF (миграции).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BackupAppDbContext>
{
    public BackupAppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackupAppDbContext>();
        optionsBuilder.UseSqlite("Data Source=backupapp.db");

        return new BackupAppDbContext(optionsBuilder.Options);
    }
}

























